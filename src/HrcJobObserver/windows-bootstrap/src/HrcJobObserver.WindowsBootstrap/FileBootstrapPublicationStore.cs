using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace HrcJobObserver.WindowsBootstrap;

internal enum FilePublicationStage
{
    DirectoryValidated,
    TempCreated,
    TempWritten,
    TempFlushed,
    TempValidated,
    BeforeRename,
    AfterRename,
    FinalOpened,
    FinalValidated,
    BeforeDisposition,
    AfterDisposition,
    RemovalHandleClosed,
    BeforeAbsenceCheck,
    ReaderOpened,
    ReaderValidated,
}

/// <summary>
/// Publishes one canonical public descriptor in an already-existing, externally
/// provisioned private NTFS directory. This type does not create or establish
/// the provenance of that directory.
/// </summary>
internal sealed class FileBootstrapPublicationStore :
    IBootstrapPublicationPublisher,
    IDisposable
{
    internal const string FinalFileName = "endpoint-v1.bin";
    private const int TempNameAttempts = 8;
    private readonly object gate = new();
    private readonly GuardedDescriptorDirectory directory;
    private readonly Func<string> tempNameFactory;
    private readonly Action<FilePublicationStage>? testHook;
    private FilePublicationLease? activeLease;
    private bool terminallyFaulted;
    private bool disposed;

    internal FileBootstrapPublicationStore(
        string existingDirectoryPath,
        string expectedOwnerSid)
        : this(
            existingDirectoryPath,
            expectedOwnerSid,
            static () => "endpoint-v1-" + Guid.NewGuid().ToString("N") + ".tmp",
            testHook: null)
    {
    }

    internal FileBootstrapPublicationStore(
        string existingDirectoryPath,
        string expectedOwnerSid,
        Func<string> tempNameFactory,
        Action<FilePublicationStage>? testHook)
    {
        ArgumentNullException.ThrowIfNull(tempNameFactory);
        this.tempNameFactory = tempNameFactory;
        this.testHook = testHook;
        directory = GuardedDescriptorDirectory.Open(
            existingDirectoryPath,
            expectedOwnerSid,
            testHook);
    }

    public ValueTask<BootstrapPublishResult> TryPublishAsync(
        ReadOnlyMemory<byte> canonicalDescriptor,
        MonotonicDeadline deadline,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _ = deadline.GetRemaining();
        byte[]? canonical = GuardedDescriptorDirectory.CanonicalClone(
            canonicalDescriptor.Span);
        try
        {
            lock (gate)
            {
                ThrowIfDisposed();
                if (terminallyFaulted)
                {
                    throw new InvalidOperationException(
                        "The file publisher has an indeterminate terminal state.");
                }

                if (activeLease is not null)
                {
                    directory.ValidateFinalIdentity(activeLease.File);
                    cancellationToken.ThrowIfCancellationRequested();
                    _ = deadline.GetRemaining();
                    return ValueTask.FromResult(
                        BootstrapPublishResult.Occupied());
                }

                cancellationToken.ThrowIfCancellationRequested();
                _ = deadline.GetRemaining();
                if (directory.TryOpenValidatedFinal(out SafeFileHandle? occupied))
                {
                    occupied!.Dispose();
                    cancellationToken.ThrowIfCancellationRequested();
                    _ = deadline.GetRemaining();
                    return ValueTask.FromResult(BootstrapPublishResult.Occupied());
                }

                for (int attempt = 0; attempt < TempNameAttempts; attempt++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    _ = deadline.GetRemaining();
                    string tempName = ValidateTempName(tempNameFactory());
                    SafeFileHandle? owned = directory.TryCreateTemp(tempName);
                    if (owned is null)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        _ = deadline.GetRemaining();
                        continue;
                    }

                    bool renamed = false;
                    try
                    {
                        testHook?.Invoke(FilePublicationStage.TempCreated);
                        cancellationToken.ThrowIfCancellationRequested();
                        _ = deadline.GetRemaining();
                        GuardedDescriptorDirectory.WriteExact(owned, canonical);
                        testHook?.Invoke(FilePublicationStage.TempWritten);
                        GuardedDescriptorDirectory.Flush(owned);
                        testHook?.Invoke(FilePublicationStage.TempFlushed);
                        directory.ValidateDescriptorFile(
                            owned,
                            tempName,
                            canonical,
                            requireFinalName: false);
                        testHook?.Invoke(FilePublicationStage.TempValidated);
                        cancellationToken.ThrowIfCancellationRequested();
                        _ = deadline.GetRemaining();
                        testHook?.Invoke(FilePublicationStage.BeforeRename);
                        if (!directory.TryRenameNoReplace(owned, FinalFileName))
                        {
                            directory.DeleteOwnedAndVerifyAbsent(owned, tempName);
                            owned = null;
                            if (!directory.TryOpenValidatedFinal(
                                    out SafeFileHandle? collision))
                            {
                                throw new SecurityException(
                                    "The final descriptor collision disappeared before validation.");
                            }

                            collision!.Dispose();
                            cancellationToken.ThrowIfCancellationRequested();
                            _ = deadline.GetRemaining();
                            return ValueTask.FromResult(
                                BootstrapPublishResult.Occupied());
                        }

                        renamed = true;
                        testHook?.Invoke(FilePublicationStage.AfterRename);
                        cancellationToken.ThrowIfCancellationRequested();
                        _ = deadline.GetRemaining();
                        using (SafeFileHandle reopened = directory.OpenFinalRequired())
                        {
                            testHook?.Invoke(FilePublicationStage.FinalOpened);
                            directory.RequireSameIdentity(owned, reopened);
                            directory.ValidateDescriptorFile(
                                reopened,
                                FinalFileName,
                                canonical,
                                requireFinalName: true);
                        }

                        testHook?.Invoke(FilePublicationStage.FinalValidated);
                        directory.ValidateFinalIdentity(owned);
                        cancellationToken.ThrowIfCancellationRequested();
                        _ = deadline.GetRemaining();
                        FilePublicationLease lease = new(this, owned);
                        activeLease = lease;
                        owned = null;
                        return ValueTask.FromResult(
                            BootstrapPublishResult.Published(lease));
                    }
                    catch (Exception primary)
                    {
                        if (owned is null)
                        {
                            throw;
                        }

                        try
                        {
                            directory.DeleteOwnedAndVerifyAbsent(
                                owned,
                                renamed ? FinalFileName : tempName);
                            owned = null;
                        }
                        catch (Exception cleanup)
                        {
                            throw new AggregateException(primary, cleanup);
                        }

                        throw;
                    }
                    finally
                    {
                        owned?.Dispose();
                    }
                }

                throw new IOException(
                    "Could not allocate a unique descriptor temporary file.");
            }
        }
        finally
        {
            if (canonical is not null)
            {
                CryptographicOperations.ZeroMemory(canonical);
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            if (activeLease is not null)
            {
                throw new InvalidOperationException(
                    "The file publisher cannot be disposed while a lease is active.");
            }

            disposed = true;
            directory.Dispose();
        }
    }

    private ValueTask<BootstrapPublicationRemovalStatus> RemoveAsync(
        FilePublicationLease lease,
        SafeFileHandle file,
        MonotonicDeadline deadline)
    {
        lock (gate)
        {
            ThrowIfDisposed();
            if (!ReferenceEquals(activeLease, lease))
            {
                throw new InvalidOperationException(
                    "The lease no longer owns this publisher's exact publication.");
            }

            try
            {
                bool expiredBefore = deadline.IsExpired();
                directory.ValidateFinalIdentity(file);
                testHook?.Invoke(FilePublicationStage.BeforeDisposition);
                GuardedDescriptorDirectory.MarkPosixDelete(file);
                testHook?.Invoke(FilePublicationStage.AfterDisposition);
                file.Dispose();
                testHook?.Invoke(FilePublicationStage.RemovalHandleClosed);
                testHook?.Invoke(FilePublicationStage.BeforeAbsenceCheck);
                directory.RequireNameAbsent(FinalFileName);
                bool expiredAfter = deadline.IsExpired();
                activeLease = null;
                return ValueTask.FromResult(
                    expiredBefore || expiredAfter
                        ? BootstrapPublicationRemovalStatus.RemovedAfterDeadline
                        : BootstrapPublicationRemovalStatus.Removed);
            }
            catch
            {
                // Removal failure is cached by the lease and cannot be retried.
                // Release this process's handles without claiming absence, but
                // keep the publisher terminal so it cannot adopt or replace an
                // indeterminate fixed-name state.
                file.Dispose();
                activeLease = null;
                terminallyFaulted = true;
                throw;
            }
        }
    }

    private static string ValidateTempName(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length > 100 ||
            value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !string.Equals(value, Path.GetFileName(value), StringComparison.Ordinal) ||
            string.Equals(value, FinalFileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The temporary descriptor name is invalid.",
                nameof(value));
        }

        return value;
    }

    private void ThrowIfDisposed()
    {
        if (disposed)
        {
            throw new ObjectDisposedException(nameof(FileBootstrapPublicationStore));
        }
    }

    private sealed class FilePublicationLease : BootstrapPublicationLease
    {
        private readonly FileBootstrapPublicationStore owner;
        private readonly SafeFileHandle file;

        internal FilePublicationLease(
            FileBootstrapPublicationStore owner,
            SafeFileHandle file)
        {
            this.owner = owner;
            this.file = file;
        }

        internal SafeFileHandle File => file;

        protected internal override ValueTask<BootstrapPublicationRemovalStatus>
            RemoveExactCoreAsync(MonotonicDeadline deadline)
        {
            return owner.RemoveAsync(this, file, deadline);
        }
    }
}

/// <summary>
/// Reads the fixed public descriptor name from an already-existing, externally
/// provisioned private NTFS directory. Structural validation is not HMAC
/// authentication.
/// </summary>
internal sealed class FileBootstrapPublicationReader : IDisposable
{
    private readonly object gate = new();
    private readonly GuardedDescriptorDirectory directory;
    private bool disposed;

    internal FileBootstrapPublicationReader(
        string existingDirectoryPath,
        string expectedOwnerSid)
        : this(existingDirectoryPath, expectedOwnerSid, testHook: null)
    {
    }

    internal FileBootstrapPublicationReader(
        string existingDirectoryPath,
        string expectedOwnerSid,
        Action<FilePublicationStage>? testHook)
    {
        directory = GuardedDescriptorDirectory.Open(
            existingDirectoryPath,
            expectedOwnerSid,
            testHook);
    }

    internal bool TryRead(out BootstrapPublicationSnapshot? snapshot)
    {
        lock (gate)
        {
            if (disposed)
            {
                throw new ObjectDisposedException(
                    nameof(FileBootstrapPublicationReader));
            }

            if (!directory.TryOpenFinal(out SafeFileHandle? file))
            {
                snapshot = null;
                return false;
            }

            SafeFileHandle opened = file!;
            using (opened)
            {
                directory.InvokeTestHook(FilePublicationStage.ReaderOpened);
                byte[] bytes = directory.ValidateAndReadFinal(opened);
                snapshot = new BootstrapPublicationSnapshot(bytes);
                return true;
            }
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            directory.Dispose();
        }
    }
}

internal sealed class GuardedDescriptorDirectory : IDisposable
{
    private const int MaximumDirectoryEnumerationPages = 16;
    private const uint DirectoryOpenAccess =
        NativeMethods.FileListDirectory |
        NativeMethods.FileTraverse |
        NativeMethods.FileReadAttributes |
        NativeMethods.ReadControl |
        NativeMethods.Synchronize;
    private const uint FileReadAccess =
        NativeMethods.GenericRead |
        NativeMethods.ReadControl;
    private readonly SafeFileHandle directory;
    private readonly string path;
    private readonly string expectedSecurityDescriptor;
    private readonly NativeFileIdentity directoryIdentity;
    private readonly Action<FilePublicationStage>? testHook;

    private GuardedDescriptorDirectory(
        SafeFileHandle directory,
        string path,
        string expectedSecurityDescriptor,
        NativeFileIdentity directoryIdentity,
        Action<FilePublicationStage>? testHook)
    {
        this.directory = directory;
        this.path = path;
        this.expectedSecurityDescriptor = expectedSecurityDescriptor;
        this.directoryIdentity = directoryIdentity;
        this.testHook = testHook;
    }

    internal static GuardedDescriptorDirectory Open(
        string existingDirectoryPath,
        string expectedOwnerSid,
        Action<FilePublicationStage>? testHook)
    {
        string path = CanonicalLocalPath(existingDirectoryPath);
        string sid = CanonicalSid(expectedOwnerSid);
        using (ProcessIdentityLease current = ProcessIdentityLease.Capture(
                   checked((uint)Environment.ProcessId)))
        {
            if (!string.Equals(
                    sid,
                    current.UserSid,
                    StringComparison.Ordinal))
            {
                throw new SecurityException(
                    "The descriptor owner SID is not the current process user.");
            }
        }

        string expectedDescriptor = CanonicalSecurityDescriptor(sid);
        // Deliberately omit FILE_SHARE_DELETE. The retained handle pins this
        // externally provisioned namespace for the lifetime of the store or
        // reader, so its owner cannot rename or remove the directory midway
        // through a guarded operation.
        SafeFileHandle directory = OpenHandle(
            path,
            DirectoryOpenAccess,
            NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
            NativeMethods.OpenExisting,
            NativeMethods.FileFlagBackupSemantics |
                NativeMethods.FileFlagOpenReparsePoint,
            securityAttributes: 0);
        try
        {
            RequireDirectory(directory);
            RequireFinalPath(directory, path);
            RequireLocalNtfsPosixVolume(directory);
            RequireSecurity(directory, expectedDescriptor);
            NativeFileIdentity identity = ReadIdentity(directory);
            testHook?.Invoke(FilePublicationStage.DirectoryValidated);
            return new GuardedDescriptorDirectory(
                directory,
                path,
                expectedDescriptor,
                identity,
                testHook);
        }
        catch
        {
            directory.Dispose();
            throw;
        }
    }

    internal static byte[] CanonicalClone(ReadOnlySpan<byte> descriptor)
    {
        byte[] source = descriptor.ToArray();
        try
        {
            BootstrapDescriptor parsed = BootstrapDescriptor.Parse(source);
            byte[] canonical = parsed.EncodeCanonical();
            if (!canonical.AsSpan().SequenceEqual(source))
            {
                CryptographicOperations.ZeroMemory(canonical);
                throw new ArgumentException(
                    "The descriptor must use its canonical encoding.",
                    nameof(descriptor));
            }

            return canonical;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(source);
        }
    }

    internal void InvokeTestHook(FilePublicationStage stage)
    {
        testHook?.Invoke(stage);
    }

    internal SafeFileHandle? TryCreateTemp(string name)
    {
        RequireDirectoryUnchanged();
        string tempPath = ChildPath(name);
        using OwnedSecurityDescriptor descriptor =
            OwnedSecurityDescriptor.Create(expectedSecurityDescriptor);
        NativeMethods.SecurityAttributes attributes = new()
        {
            Length = checked((uint)Marshal.SizeOf<NativeMethods.SecurityAttributes>()),
            SecurityDescriptor = descriptor.Pointer,
            InheritHandle = 0,
        };
        nint raw;
        unsafe
        {
            raw = NativeMethods.CreateFile(
                tempPath,
                FileReadAccess |
                    NativeMethods.GenericWrite |
                    NativeMethods.DeleteAccess,
                NativeMethods.FileShareRead,
                (nint)(&attributes),
                NativeMethods.CreateNew,
                NativeMethods.FileAttributeNormal |
                    NativeMethods.FileFlagOpenReparsePoint,
                0);
        }

        SafeFileHandle file = new(raw, true);
        if (file.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            file.Dispose();
            if (error is NativeMethods.ErrorFileExists or
                NativeMethods.ErrorAlreadyExists)
            {
                return null;
            }

            throw new Win32Exception(error, "Creating the descriptor temp file failed.");
        }

        try
        {
            RequireRegularFile(file);
            RequireSecurity(file, expectedSecurityDescriptor);
            RequireDirectoryUnchanged();
            RequireFinalPath(file, tempPath);
            RequireSameVolume(directory, file);
            RequireDirectoryEntryIdentity(name, file);
            return file;
        }
        catch (Exception primary)
        {
            try
            {
                DeleteOwnedAndVerifyAbsent(file, name);
            }
            catch (Exception cleanup)
            {
                throw new AggregateException(primary, cleanup);
            }
            finally
            {
                file.Dispose();
            }

            throw;
        }
    }

    internal bool TryRenameNoReplace(SafeFileHandle file, string finalName)
    {
        RequireDirectoryUnchanged();
        byte[] nameBytes = System.Text.Encoding.Unicode.GetBytes(finalName);
        int rootOffset = IntPtr.Size == 8 ? 8 : 4;
        int lengthOffset = rootOffset + IntPtr.Size;
        int nameOffset = lengthOffset + sizeof(uint);
        int nativeHeaderSize = IntPtr.Size == 8 ? 24 : 16;
        int size = checked(nativeHeaderSize + nameBytes.Length);
        nint buffer = Marshal.AllocHGlobal(size);
        bool added = false;
        try
        {
            unsafe
            {
                new Span<byte>((void*)buffer, size).Clear();
            }

            Marshal.WriteInt32(buffer, 0, 0);
            directory.DangerousAddRef(ref added);
            Marshal.WriteIntPtr(
                buffer,
                rootOffset,
                directory.DangerousGetHandle());
            Marshal.WriteInt32(buffer, lengthOffset, nameBytes.Length);
            Marshal.Copy(nameBytes, 0, buffer + nameOffset, nameBytes.Length);
            int status = NativeMethods.NtSetInformationFile(
                    file,
                    out _,
                    buffer,
                    checked((uint)size),
                    NativeMethods.FileRenameInformationClass);
            return ClassifyRenameResult(status);
        }
        finally
        {
            if (added)
            {
                directory.DangerousRelease();
            }

            CryptographicOperations.ZeroMemory(nameBytes);
            Marshal.FreeHGlobal(buffer);
        }
    }

    internal static bool ClassifyRenameResult(int functionStatus)
    {
        const int StatusSuccess = 0;
        const int StatusObjectNameCollision = unchecked((int)0xC0000035);
        if (functionStatus == StatusObjectNameCollision)
        {
            return false;
        }

        if (functionStatus != StatusSuccess)
        {
            throw new IOException(
                $"Publishing the descriptor name failed (NTSTATUS " +
                $"0x{unchecked((uint)functionStatus):X8}).");
        }

        return true;
    }

    internal bool TryOpenValidatedFinal(out SafeFileHandle? file)
    {
        if (!TryOpenFinal(out file))
        {
            return false;
        }

        try
        {
            byte[] bytes = ValidateAndReadFinal(file!);
            CryptographicOperations.ZeroMemory(bytes);
            return true;
        }
        catch
        {
            file!.Dispose();
            file = null;
            throw;
        }
    }

    internal bool TryOpenFinal(out SafeFileHandle? file)
    {
        RequireDirectoryUnchanged();
        NativeFileIdentity? expectedIdentity = FindDirectoryEntryIdentity(
            FileBootstrapPublicationStore.FinalFileName);
        if (expectedIdentity is null)
        {
            file = null;
            return false;
        }

        string finalPath = ChildPath(FileBootstrapPublicationStore.FinalFileName);
        nint raw = NativeMethods.CreateFile(
            finalPath,
            FileReadAccess,
            NativeMethods.FileShareRead |
                NativeMethods.FileShareWrite |
                NativeMethods.FileShareDelete,
            0,
            NativeMethods.OpenExisting,
            NativeMethods.FileFlagOpenReparsePoint,
            0);
        file = new SafeFileHandle(raw, true);
        if (file.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            file.Dispose();
            file = null;
            throw new Win32Exception(error, "Opening the public descriptor failed.");
        }

        try
        {
            RequireDirectoryUnchanged();
            if (!expectedIdentity.Value.Equals(ReadIdentity(file)))
            {
                throw new SecurityException(
                    "The descriptor name resolved to a different file identity.");
            }
        }
        catch
        {
            file.Dispose();
            file = null;
            throw;
        }

        return true;
    }

    internal SafeFileHandle OpenFinalRequired()
    {
        if (!TryOpenFinal(out SafeFileHandle? file))
        {
            throw new SecurityException(
                "The published descriptor disappeared before verification.");
        }

        return file!;
    }

    internal byte[] ValidateAndReadFinal(SafeFileHandle file)
    {
        byte[] bytes = ReadCanonical(file);
        try
        {
            ValidateDescriptorFile(
                file,
                FileBootstrapPublicationStore.FinalFileName,
                bytes,
                requireFinalName: true);
            testHook?.Invoke(FilePublicationStage.ReaderValidated);
            return bytes;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw;
        }
    }

    internal void ValidateDescriptorFile(
        SafeFileHandle file,
        string expectedName,
        ReadOnlySpan<byte> expectedBytes,
        bool requireFinalName)
    {
        RequireDirectoryUnchanged();
        RequireRegularFile(file);
        RequireSecurity(file, expectedSecurityDescriptor);
        RequireSameVolume(directory, file);
        RequireFinalPath(file, ChildPath(expectedName));
        byte[] actual = ReadCanonical(file);
        try
        {
            if (!actual.AsSpan().SequenceEqual(expectedBytes))
            {
                throw new SecurityException(
                    "The descriptor bytes changed during publication.");
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(actual);
        }

        if (requireFinalName &&
            !string.Equals(
                expectedName,
                FileBootstrapPublicationStore.FinalFileName,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The final descriptor name is invalid.");
        }
    }

    internal void ValidateFinalIdentity(SafeFileHandle retained)
    {
        using SafeFileHandle reopened = OpenFinalRequired();
        RequireSameIdentity(retained, reopened);
        byte[] bytes = ValidateAndReadFinal(reopened);
        CryptographicOperations.ZeroMemory(bytes);
    }

    internal void RequireSameIdentity(
        SafeFileHandle expected,
        SafeFileHandle actual)
    {
        if (!ReadIdentity(expected).Equals(ReadIdentity(actual)))
        {
            throw new SecurityException(
                "The descriptor file identity changed.");
        }
    }

    internal static unsafe void WriteExact(
        SafeFileHandle file,
        ReadOnlySpan<byte> bytes)
    {
        fixed (byte* pointer = bytes)
        {
            if (NativeMethods.WriteFile(
                    file,
                    pointer,
                    checked((uint)bytes.Length),
                    out uint written,
                    0) == 0)
            {
                throw NativeMethods.Win32Failure(
                    "Writing the public descriptor failed");
            }

            if (written != bytes.Length)
            {
                throw new IOException(
                    "Writing the public descriptor completed with a short count.");
            }
        }
    }

    internal static void Flush(SafeFileHandle file)
    {
        if (NativeMethods.FlushFileBuffers(file) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Flushing the public descriptor failed");
        }
    }

    internal void DeleteOwnedAndVerifyAbsent(
        SafeFileHandle file,
        string name)
    {
        MarkPosixDelete(file);
        file.Dispose();
        RequireNameAbsent(name);
    }

    internal static unsafe void MarkPosixDelete(SafeFileHandle file)
    {
        NativeMethods.FileDispositionInfoEx information = new()
        {
            Flags = NativeMethods.FileDispositionDelete |
                NativeMethods.FileDispositionPosixSemantics,
        };
        if (NativeMethods.SetFileInformationByHandle(
                file,
                NativeMethods.FileDispositionInfoExClass,
                (nint)(&information),
                checked((uint)sizeof(NativeMethods.FileDispositionInfoEx))) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Marking the exact descriptor file for removal failed");
        }
    }

    internal void RequireNameAbsent(string name)
    {
        RequireDirectoryUnchanged();
        if (FindDirectoryEntryIdentity(name) is not null)
        {
            throw new SecurityException(
                "The exact descriptor name remained visible after removal.");
        }

        RequireDirectoryUnchanged();
    }

    public void Dispose()
    {
        directory.Dispose();
    }

    private void RequireDirectoryUnchanged()
    {
        RequireDirectory(directory);
        RequireFinalPath(directory, path);
        RequireSecurity(directory, expectedSecurityDescriptor);
        if (!directoryIdentity.Equals(ReadIdentity(directory)))
        {
            throw new SecurityException(
                "The guarded descriptor directory identity changed.");
        }
    }

    private string ChildPath(string name)
    {
        return Path.Combine(path, name);
    }

    private static SafeFileHandle OpenHandle(
        string path,
        uint access,
        uint share,
        uint disposition,
        uint flags,
        nint securityAttributes)
    {
        nint raw = NativeMethods.CreateFile(
            path,
            access,
            share,
            securityAttributes,
            disposition,
            flags,
            0);
        SafeFileHandle handle = new(raw, true);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw NativeMethods.Win32Failure("Opening a guarded descriptor path failed");
        }

        return handle;
    }

    private static string CanonicalLocalPath(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string full = Path.GetFullPath(value);
        if (!Path.IsPathFullyQualified(full) ||
            full.Length < 3 ||
            !char.IsAsciiLetter(full[0]) ||
            full[1] != ':' ||
            full[2] != Path.DirectorySeparatorChar ||
            full.IndexOf(':', 2) >= 0)
        {
            throw new ArgumentException(
                "The descriptor directory must be a local drive path.",
                nameof(value));
        }

        return Path.TrimEndingDirectorySeparator(full);
    }

    private static string CanonicalSid(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        SecurityIdentifier sid = new(value);
        if (!string.Equals(sid.Value, value, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The owner SID must be canonical.",
                nameof(value));
        }

        return sid.Value;
    }

    private static string CanonicalSecurityDescriptor(string ownerSid)
    {
        using OwnedSecurityDescriptor descriptor = OwnedSecurityDescriptor.Create(
            "O:" + ownerSid + "D:P(A;;FA;;;SY)(A;;FA;;;" + ownerSid + ")");
        return descriptor.Canonical;
    }

    private static void RequireDirectory(SafeFileHandle handle)
    {
        NativeMethods.FileAttributeTagInfo information =
            ReadAttributeInformation(handle);
        if ((information.FileAttributes &
                NativeMethods.FileAttributeDirectory) == 0 ||
            (information.FileAttributes &
                NativeMethods.FileAttributeReparsePoint) != 0)
        {
            throw new SecurityException(
                "The guarded descriptor path is not a non-reparse directory.");
        }
    }

    private static void RequireRegularFile(SafeFileHandle handle)
    {
        NativeMethods.FileAttributeTagInfo attributes =
            ReadAttributeInformation(handle);
        NativeMethods.FileStandardInfo standard =
            ReadStandardInformation(handle);
        if ((attributes.FileAttributes &
                (NativeMethods.FileAttributeDirectory |
                    NativeMethods.FileAttributeReparsePoint)) != 0 ||
            standard.Directory != 0 ||
            standard.DeletePending != 0 ||
            standard.NumberOfLinks != 1)
        {
            throw new SecurityException(
                "The descriptor is not a single-link regular file.");
        }
    }

    private static unsafe NativeMethods.FileAttributeTagInfo
        ReadAttributeInformation(SafeFileHandle handle)
    {
        NativeMethods.FileAttributeTagInfo information = new();
        if (NativeMethods.GetFileInformationByHandleEx(
                handle,
                NativeMethods.FileAttributeTagInfoClass,
                &information,
                checked((uint)sizeof(NativeMethods.FileAttributeTagInfo))) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading descriptor attributes failed");
        }

        return information;
    }

    private static unsafe NativeMethods.FileStandardInfo
        ReadStandardInformation(SafeFileHandle handle)
    {
        NativeMethods.FileStandardInfo information = new();
        if (NativeMethods.GetFileInformationByHandleEx(
                handle,
                NativeMethods.FileStandardInfoClass,
                &information,
                checked((uint)sizeof(NativeMethods.FileStandardInfo))) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading descriptor link information failed");
        }

        return information;
    }

    private static unsafe NativeFileIdentity ReadIdentity(SafeFileHandle handle)
    {
        NativeMethods.FileIdInfo information = new();
        if (NativeMethods.GetFileInformationByHandleEx(
                handle,
                NativeMethods.FileIdInfoClass,
                &information,
                checked((uint)sizeof(NativeMethods.FileIdInfo))) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading descriptor identity failed");
        }

        byte[] identifier = new byte[16];
        byte* source = information.FileId.Identifier;
        new ReadOnlySpan<byte>(source, identifier.Length).CopyTo(identifier);

        return new NativeFileIdentity(
            information.VolumeSerialNumber,
            identifier);
    }

    private static void RequireSameVolume(
        SafeFileHandle expected,
        SafeFileHandle actual)
    {
        if (ReadIdentity(expected).VolumeSerialNumber !=
            ReadIdentity(actual).VolumeSerialNumber)
        {
            throw new SecurityException(
                "The descriptor crossed the guarded directory volume.");
        }
    }

    private static unsafe void RequireLocalNtfsPosixVolume(
        SafeFileHandle handle)
    {
        const int BufferChars = 64;
        char* volume = stackalloc char[BufferChars];
        char* fileSystem = stackalloc char[BufferChars];
        if (NativeMethods.GetVolumeInformationByHandle(
                handle,
                volume,
                BufferChars,
                out uint serial,
                out uint componentLength,
                out uint flags,
                fileSystem,
                BufferChars) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading the guarded directory volume failed");
        }

        _ = serial;
        _ = componentLength;
        string fileSystemName = new(fileSystem);
        if (!string.Equals(fileSystemName, "NTFS", StringComparison.Ordinal) ||
            (flags & NativeMethods.FileSupportsPosixUnlinkRename) == 0)
        {
            throw new PlatformNotSupportedException(
                "The descriptor directory requires local NTFS POSIX unlink support.");
        }

        uint guidRequired = NativeMethods.GetFinalPathNameByHandle(
            handle,
            null,
            0,
            NativeMethods.VolumeNameGuid);
        if (guidRequired == 0 || guidRequired > 32_768)
        {
            throw new PlatformNotSupportedException(
                "The descriptor directory is not on a local Mount Manager volume.");
        }

        char[] guidBuffer = new char[checked((int)guidRequired)];
        fixed (char* guidPointer = guidBuffer)
        {
            uint guidWritten = NativeMethods.GetFinalPathNameByHandle(
                handle,
                guidPointer,
                checked((uint)guidBuffer.Length),
                NativeMethods.VolumeNameGuid);
            if (guidWritten == 0 || guidWritten >= guidBuffer.Length)
            {
                throw new PlatformNotSupportedException(
                    "The descriptor directory is not on a local Mount Manager volume.");
            }

            string guidPath = new(
                guidPointer,
                0,
                checked((int)guidWritten));
            if (!guidPath.StartsWith(
                    "\\\\?\\Volume{",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new PlatformNotSupportedException(
                    "The descriptor directory is not on a local Mount Manager volume.");
            }
        }
    }

    private void RequireDirectoryEntryIdentity(
        string name,
        SafeFileHandle expectedFile)
    {
        NativeFileIdentity? found = FindDirectoryEntryIdentity(name);
        if (found is null || !found.Value.Equals(ReadIdentity(expectedFile)))
        {
            throw new SecurityException(
                "The retained directory does not contain the expected file identity.");
        }
    }

    private unsafe NativeFileIdentity? FindDirectoryEntryIdentity(string name)
    {
        const int BufferBytes = 64 * 1024;
        const int FileNameLengthOffset = 60;
        const int FileIdOffset = 72;
        const int FileNameOffset = 88;
        byte[] buffer = new byte[BufferBytes];
        try
        {
            fixed (byte* pointer = buffer)
            {
                bool restart = true;
                int pageCount = 0;
                while (true)
                {
                    if (pageCount == MaximumDirectoryEnumerationPages)
                    {
                        throw new SecurityException(
                            "The guarded descriptor directory contains too many entries.");
                    }

                    pageCount++;
                    Array.Clear(buffer);
                    if (NativeMethods.GetFileInformationByHandleEx(
                            directory,
                            restart
                                ? NativeMethods.FileIdExtdDirectoryRestartInfoClass
                                : NativeMethods.FileIdExtdDirectoryInfoClass,
                            pointer,
                            BufferBytes) == 0)
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == NativeMethods.ErrorNoMoreFiles)
                        {
                            return null;
                        }

                        throw new Win32Exception(
                            error,
                            "Enumerating the retained descriptor directory failed.");
                    }

                    restart = false;
                    int offset = 0;
                    while (true)
                    {
                        if (offset < 0 || offset > BufferBytes - FileNameOffset)
                        {
                            throw new SecurityException(
                                "The retained directory enumeration was malformed.");
                        }

                        uint nextOffset = *(uint*)(pointer + offset);
                        uint nameLength = *(uint*)(pointer + offset +
                            FileNameLengthOffset);
                        if ((nameLength & 1) != 0 ||
                            nameLength > BufferBytes - offset - FileNameOffset)
                        {
                            throw new SecurityException(
                                "The retained directory entry name was malformed.");
                        }

                        string entryName = new(
                            (char*)(pointer + offset + FileNameOffset),
                            0,
                            checked((int)(nameLength / sizeof(char))));
                        if (string.Equals(
                                entryName,
                                name,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            byte[] identifier = new byte[16];
                            new ReadOnlySpan<byte>(
                                    pointer + offset + FileIdOffset,
                                    identifier.Length)
                                .CopyTo(identifier);
                            return new NativeFileIdentity(
                                directoryIdentity.VolumeSerialNumber,
                                identifier);
                        }

                        if (nextOffset == 0)
                        {
                            break;
                        }

                        if (nextOffset < FileNameOffset ||
                            nextOffset > BufferBytes - offset)
                        {
                            throw new SecurityException(
                                "The retained directory enumeration offset was malformed.");
                        }

                        offset += checked((int)nextOffset);
                    }
                }
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(buffer);
        }
    }

    private static unsafe void RequireFinalPath(
        SafeFileHandle handle,
        string expectedPath)
    {
        uint required = NativeMethods.GetFinalPathNameByHandle(
            handle,
            null,
            0,
            0);
        if (required == 0 || required > 32_768)
        {
            throw NativeMethods.Win32Failure(
                "Reading the guarded descriptor final path failed");
        }

        char[] buffer = new char[checked((int)required)];
        fixed (char* pointer = buffer)
        {
            uint written = NativeMethods.GetFinalPathNameByHandle(
                handle,
                pointer,
                checked((uint)buffer.Length),
                0);
            if (written == 0 || written >= buffer.Length)
            {
                throw NativeMethods.Win32Failure(
                    "Reading the guarded descriptor final path failed");
            }

            string actual = new(pointer, 0, checked((int)written));
            string expected = expectedPath.StartsWith("\\\\?\\", StringComparison.Ordinal)
                ? expectedPath
                : "\\\\?\\" + expectedPath;
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new SecurityException(
                    "The guarded descriptor path resolved to an unexpected target.");
            }
        }
    }

    private static void RequireSecurity(
        SafeFileHandle handle,
        string expectedDescriptor)
    {
        string applied = ReadSecurityDescriptor(handle);
        if (!string.Equals(
                applied,
                expectedDescriptor,
                StringComparison.Ordinal))
        {
            throw new SecurityException(
                "The descriptor owner or protected DACL is not exact.");
        }
    }

    private static string ReadSecurityDescriptor(SafeFileHandle handle)
    {
        bool added = false;
        nint securityDescriptor = 0;
        uint result;
        try
        {
            handle.DangerousAddRef(ref added);
            result = NativeMethods.GetSecurityInfo(
                handle.DangerousGetHandle(),
                NativeMethods.SeFileObject,
                NativeMethods.OwnerSecurityInformation |
                    NativeMethods.DaclSecurityInformation,
                out nint owner,
                out nint group,
                out nint dacl,
                out nint sacl,
                out securityDescriptor);
            _ = owner;
            _ = group;
            _ = dacl;
            _ = sacl;
        }
        finally
        {
            if (added)
            {
                handle.DangerousRelease();
            }
        }

        if (result != NativeMethods.ErrorSuccess || securityDescriptor == 0)
        {
            throw new Win32Exception(
                checked((int)result),
                "Reading descriptor security failed.");
        }

        try
        {
            return SecurityDescriptorToString(securityDescriptor);
        }
        finally
        {
            _ = NativeMethods.LocalFree(securityDescriptor);
        }
    }

    private static string SecurityDescriptorToString(nint descriptor)
    {
        if (NativeMethods.ConvertSecurityDescriptorToString(
                descriptor,
                NativeMethods.SddlRevision1,
                NativeMethods.OwnerSecurityInformation |
                    NativeMethods.DaclSecurityInformation,
                out nint text,
                out uint length) == 0 ||
            text == 0 || length == 0)
        {
            throw NativeMethods.Win32Failure(
                "Canonicalising descriptor security failed");
        }

        try
        {
            return Marshal.PtrToStringUni(text) ??
                throw new SecurityException(
                    "The descriptor security string was empty.");
        }
        finally
        {
            _ = NativeMethods.LocalFree(text);
        }
    }

    private static byte[] ReadCanonical(SafeFileHandle file)
    {
        if (NativeMethods.GetFileSizeEx(file, out long size) == 0)
        {
            throw NativeMethods.Win32Failure(
                "Reading descriptor size failed");
        }

        if (size <= 0 || size > BootstrapDescriptor.MaximumEncodedLength)
        {
            throw new InvalidDataException(
                "The public descriptor length is invalid.");
        }

        if (NativeMethods.SetFilePointerEx(
                file,
                0,
                out long position,
                NativeMethods.FileBegin) == 0 ||
            position != 0)
        {
            throw NativeMethods.Win32Failure(
                "Seeking the public descriptor failed");
        }

        byte[] bytes = new byte[checked((int)size)];
        try
        {
            unsafe
            {
                fixed (byte* pointer = bytes)
                {
                    if (NativeMethods.ReadFile(
                            file,
                            pointer,
                            checked((uint)bytes.Length),
                            out uint read,
                            0) == 0)
                    {
                        throw NativeMethods.Win32Failure(
                            "Reading the public descriptor failed");
                    }

                    if (read != bytes.Length)
                    {
                        throw new EndOfStreamException(
                            "The public descriptor ended before its recorded length.");
                    }
                }

                byte trailing = 0;
                if (NativeMethods.ReadFile(
                        file,
                        &trailing,
                        1,
                        out uint trailingRead,
                        0) == 0)
                {
                    throw NativeMethods.Win32Failure(
                        "Checking the public descriptor terminator failed");
                }

                if (trailingRead != 0)
                {
                    throw new InvalidDataException(
                        "The public descriptor has trailing bytes.");
                }
            }

            if (NativeMethods.GetFileSizeEx(file, out long stableSize) == 0)
            {
                throw NativeMethods.Win32Failure(
                    "Rechecking descriptor size failed");
            }

            if (stableSize != size)
            {
                throw new SecurityException(
                    "The public descriptor length changed during its read.");
            }

            BootstrapDescriptor parsed = BootstrapDescriptor.Parse(bytes);
            byte[] canonical = parsed.EncodeCanonical();
            try
            {
                if (!canonical.AsSpan().SequenceEqual(bytes))
                {
                    throw new InvalidDataException(
                        "The public descriptor is not canonical.");
                }
            }
            finally
            {
                CryptographicOperations.ZeroMemory(canonical);
            }

            return bytes;
        }
        catch
        {
            CryptographicOperations.ZeroMemory(bytes);
            throw;
        }
    }

    private readonly record struct NativeFileIdentity(
        ulong VolumeSerialNumber,
        byte[] Identifier)
    {
        public bool Equals(NativeFileIdentity other)
        {
            return VolumeSerialNumber == other.VolumeSerialNumber &&
                Identifier.AsSpan().SequenceEqual(other.Identifier);
        }

        public override int GetHashCode()
        {
            HashCode code = new();
            code.Add(VolumeSerialNumber);
            foreach (byte value in Identifier)
            {
                code.Add(value);
            }

            return code.ToHashCode();
        }
    }

    private sealed class OwnedSecurityDescriptor : IDisposable
    {
        private nint pointer;

        private OwnedSecurityDescriptor(nint pointer, string canonical)
        {
            this.pointer = pointer;
            Canonical = canonical;
        }

        internal nint Pointer => pointer != 0
            ? pointer
            : throw new ObjectDisposedException(nameof(OwnedSecurityDescriptor));

        internal string Canonical { get; }

        internal static OwnedSecurityDescriptor Create(string sddl)
        {
            if (NativeMethods.ConvertStringSecurityDescriptor(
                    sddl,
                    NativeMethods.SddlRevision1,
                    out nint descriptor,
                    out uint size) == 0 ||
                descriptor == 0 || size == 0)
            {
                throw NativeMethods.Win32Failure(
                    "Creating descriptor security failed");
            }

            try
            {
                string canonical = SecurityDescriptorToString(descriptor);
                return new OwnedSecurityDescriptor(descriptor, canonical);
            }
            catch
            {
                _ = NativeMethods.LocalFree(descriptor);
                throw;
            }
        }

        public void Dispose()
        {
            nint owned = pointer;
            pointer = 0;
            if (owned != 0)
            {
                _ = NativeMethods.LocalFree(owned);
            }
        }
    }
}
