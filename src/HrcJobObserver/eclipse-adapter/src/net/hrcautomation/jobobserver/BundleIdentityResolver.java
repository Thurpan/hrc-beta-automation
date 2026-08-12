package net.hrcautomation.jobobserver;

@FunctionalInterface
interface BundleIdentityResolver {
    BundleIdentity resolve(Class<?> jobClass);
}
