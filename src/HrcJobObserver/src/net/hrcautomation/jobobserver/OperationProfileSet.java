package net.hrcautomation.jobobserver;

import java.util.Collection;
import java.util.EnumMap;
import java.util.HashMap;
import java.util.Map;
import java.util.Objects;

/** Immutable operation and Job-class index shared by capture and correlation. */
final class OperationProfileSet {
    private final Map<OperationKind, OperationProfile> byOperation;
    private final Map<String, OperationProfile> byClassName;

    OperationProfileSet(Collection<OperationProfile> profiles) {
        Objects.requireNonNull(profiles, "profiles");
        EnumMap<OperationKind, OperationProfile> operations =
                new EnumMap<>(OperationKind.class);
        Map<String, OperationProfile> classes = new HashMap<>();
        for (OperationProfile profile : profiles) {
            Objects.requireNonNull(profile, "profile");
            OperationProfile previousOperation =
                    operations.put(profile.operation(), profile);
            OperationProfile previousClass = classes.put(profile.className(), profile);
            if (previousOperation != null || previousClass != null) {
                throw new IllegalArgumentException("operation profiles must be unique");
            }
        }
        if (operations.isEmpty()) {
            throw new IllegalArgumentException("at least one operation profile is required");
        }
        byOperation = Map.copyOf(operations);
        byClassName = Map.copyOf(classes);
    }

    OperationProfile forOperation(OperationKind operation) {
        return byOperation.get(operation);
    }

    OperationProfile forClassName(String className) {
        return byClassName.get(className);
    }

    boolean contains(OperationKind operation) {
        return byOperation.containsKey(operation);
    }
}
