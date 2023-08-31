## 2.0.15 (31-08-2023):

Revert adding `VostokLoggerExtensions.CreateVostokMicrosoftLog` extension.

## 2.0.14 (31-08-2023):

Added `VostokLoggerExtensions.CreateVostokMicrosoftLog` extension to create Vostok log from ILoggerFactory.

## 2.0.13 (06-12-2021):

Added `net6.0` target.

## 2.0.11 (25.06.2020):

Scopes can now be ignored by type name prefixes.

## 2.0.10 (25.06.2020):

- Do not add event id properties by default (there's a setting for it now in VostokLoggerProviderSettings).
- Use PreciseDateTime.Now for log event timestamps.
- BeginScope: no allocations for ignored scopes.

## 2.0.9 (18.04.2020):

`MicrosoftLog` now preserves structed log properties.

## 2.0.8 (19.03.2020)

Added `MicrosoftLog` adapter.

## 2.0.7 (19.03.2020)

Added `MicrosoftLogScopes` constants.

## 2.0.6 (05.03.2020)

Fixed multiple scopes logging.

## 2.0.5 (26.02.2020)

Removed `MinimumLevel` setting.

## 2.0.4 (24.02.2020)

Added `MinimumLevel` setting.

## 2.0.3 (23.12.2019)

Create `loggerFactory.AddVostok(log)` extension method

## 2.0.2 (19.12.2019):

Added `VostokLoggerProviderSettings` that allows to ignore some scopes.

## 2.0.0 (26.06.2019):

Breaking change: BeginScope() method no longer adds a `Scope` property to log events. Instead, it utilizes well-known operation context property.

## 1.0.0 (11.03.2019):

Adapted to ForContext() in changes in abstractions module.

## 0.1.1

Downgrade version of `Microsoft.Extensions.Logging.Abstractions` to 2.0.0

## 0.1.0

* Create `VostokLoggerProvider` - an implementation of `ILoggerProvider`
* Create `loggerFactory.AddVostok(log)` extension method
* Support concurrent `ILogger.BeginScope()`
