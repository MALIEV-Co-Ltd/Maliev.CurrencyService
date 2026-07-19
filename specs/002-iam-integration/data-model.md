# Data Model: Authorization Entities

## Permissions

Permissions are static strings defined in the codebase and registered with the IAM service.

| Permission ID | Description | Category |
|---------------|-------------|----------|
| `currency.currencies.read` | Read currency details | Currency |
| `currency.currencies.list` | List all currencies | Currency |
| `currency.currencies.search` | Search currencies | Currency |
| `currency.currencies.create` | Create new currencies | Currency (Admin) |
| `currency.currencies.update` | Update currency details | Currency (Admin) |
| `currency.currencies.delete` | Delete currencies | Currency (Admin) |
| `currency.currencies.activate` | Toggle currency status | Currency (Admin) |
| `currency.rates.get` | Get current rates | Rates |
| `currency.rates.convert` | Convert amounts | Rates |
| `currency.rates.historical` | Get historical rates | Rates |
| `currency.rates.update` | Update exchange rates | Rates (Admin) |
| `currency.rates.bulk-update` | Bulk update rates | Rates (Admin) |
| `currency.rates.set-source` | Configure rate source | Rates (Admin) |
| `currency.snapshots.create` | Create rate snapshots | Snapshots |
| `currency.snapshots.read` | Read snapshot details | Snapshots |
| `currency.snapshots.list` | List snapshots | Snapshots |
| `currency.snapshots.delete` | Delete old snapshots | Snapshots |
| `currency.snapshots.export` | Export snapshot data | Snapshots |
| `currency.snapshots.audit` | View audit logs | Snapshots |
| `currency.system.refresh-rates` | Trigger rate refresh | System |
| `currency.system.rebuild-cache` | Rebuild cache | System |
| `currency.system.view-stats` | View service stats | System |
| `currency.system.configure` | System configuration | System |

## Roles

Roles are collections of permissions predefined in the service.

| Role ID | Name | Permissions Included |
|---------|------|----------------------|
| `currency-admin` | Currency Administrator | All permissions |
| `currency-manager` | Currency Manager | All except system configuration |
| `currency-operator` | Currency Operator | Snapshots, public reads, rate refresh |
| `currency-viewer` | Currency Viewer | All public read operations + snapshot read |

## Configuration

| Key | Type | Description |
|-----|------|-------------|
| `Features:PermissionBasedAuthEnabled` | Boolean | Toggle authorization enforcement |
| `ExternalServices:IAM:BaseUrl` | String | IAM service endpoint |
| `ExternalServices:IAM:Timeout` | Integer | Connection timeout (ms) |
