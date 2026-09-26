# Deployment runbook

SmartElderlyCare is an ASP.NET Core 8 MVC application backed by SQL Server via EF Core.

Read this before the first production deployment. Sections 1 and 2 are mandatory.

---

## 1. Rotate the compromised administrator credentials

**Do this first, before deploying anything.**

An earlier version of this repository committed live super-administrator credentials to
`appsettings.json`, and they remain in git history. Two older generations of the same
password are also recoverable from earlier commits. Those credentials must be treated as
public.

Rotate on every environment that ever used them:

1. Sign in as the super administrator and change the password
   (**Profile → Change Password**). This rewrites the ASP.NET Identity hash.
2. Confirm the new password works by signing out and back in.
3. The old password is no longer accepted anywhere. The offline code path that used to
   accept it has been deleted, so nothing else references `PasswordSalt`,
   `PasswordHash`, or `PasswordIterations`.

Rotating the password is sufficient to invalidate the leaked material. Rewriting git
history is optional and best done with `git filter-repo` or BFG if the repository has
been shared, but it does not remove the need to rotate.

---

## 2. Provide configuration and secrets

Configuration is layered. Later sources win:

1. `appsettings.json` (tracked, no secrets)
2. `appsettings.{Environment}.json` (gitignored in production)
3. User secrets (**Development only**)
4. Environment variables (highest precedence)

### 2.1 Provision the database

`MigrateAsync()` applies migrations but **cannot create the database**. Create it first:

```sql
CREATE DATABASE SmartElderlyCare;
```

The application login needs `db_owner` on it, because migrations issue
`CREATE TABLE` / `ALTER TABLE`.

### 2.2 Set the connection string

Do **not** put `MultipleActiveResultSets=True` in the connection string. It is
incompatible with `EnableRetryOnFailure`, which is enabled in `Program.cs`.

Prefer an environment variable on the host:

```
ConnectionStrings__DefaultConnection=Server=...;Database=SmartElderlyCare;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True
```

Alternatively copy the tracked template and fill it in:

```bash
cp appsettings.Production.example.json appsettings.Production.json
```

`appsettings.Production.json` is gitignored. Never commit it.

### 2.3 Bootstrap the first administrator

Only needed on an empty database. Set:

```
AdminUser__Name
AdminUser__Email
AdminUser__IdentityPasswordHash
```

`IdentityPasswordHash` is an ASP.NET Identity v3 password hash, **not** a plaintext
password. To generate one for a chosen password, use the Identity APIs or a small
script; do not invent a hash by hand.

Once the administrator exists, remove all three values. The application only requires
them to create the account, and it logs and continues when they are absent and an
administrator already exists.

### 2.4 Set the support contact shown on the password-recovery page

```
SupportEmail
SupportPhones      # comma separated, e.g. "+263 000 000 000, +263 000 000 001"
```

---

## 3. Local development

Local development reads `AdminUser:*` from user secrets, which are stored outside the
repository in your user profile:

```bash
dotnet user-secrets set "AdminUser:Name" "Your Name"
dotnet user-secrets set "AdminUser:Email" "you@example.com"
dotnet user-secrets set "AdminUser:IdentityPasswordHash" "<identity v3 hash>"
dotnet user-secrets list   # verify
```

The LocalDB connection string lives in the tracked `appsettings.Development.json`,
which is safe because it uses integrated security and carries no password.

To clear a lockout on a local administrator:

```bash
dotnet run -- --unlock-admin
```

`--unlock-admin` exits after unlocking and is rejected outside the Development
environment.

---

## 4. Deploy

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
dotnet publish -c Release -o ./publish
```

Then on the server:

1. Back up the database (section 5). **Do this before every deploy.**
2. Stop the running instance.
3. Copy `publish/` to the application directory.
4. Supply configuration through environment variables or `appsettings.Production.json`.
5. Start the instance. On first start, migrations apply automatically.
6. Check `GET /healthz`. It returns 200 only when the database is reachable.
7. Sign in and confirm the dashboard loads.

The application **refuses to start** in production if the database cannot be reached,
a migration fails, or no administrator exists. That is intentional: a half-initialised
schema must fail the deploy rather than serve traffic. In Development the same failures
are logged and startup continues.

> Do not set `ASPNETCORE_ENVIRONMENT=Development` on a production host. Doing so
> re-enables the fail-open startup behaviour and marks cookies as `Secure` only when
> the request already is.

---

## 5. Backup and restore

This system stores patient health records. A failed migration with no tested restore is
an unrecoverable incident.

### Backup before deploying

```sql
BACKUP DATABASE [SmartElderlyCare]
TO DISK = N'C:\Backups\SmartElderlyCare_predeploy.bak'
WITH FORMAT, COMPRESSION, STATS = 10;
```

Verify the backup is restorable, not merely present:

```sql
RESTORE VERIFYONLY FROM DISK = N'C:\Backups\SmartElderlyCare_predeploy.bak';
```

### Restore

```sql
ALTER DATABASE [SmartElderlyCare] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
RESTORE DATABASE [SmartElderlyCare]
FROM DISK = N'C:\Backups\SmartElderlyCare_predeploy.bak'
WITH MOVE N'SmartElderlyCare' TO N'<data path>\SmartElderlyCare.mdf',
     MOVE N'SmartElderlyCare_log' TO N'<log path>\SmartElderlyCare_log.ldf',
     RECOVERY;
ALTER DATABASE [SmartElderlyCare] SET MULTI_USER;
```

Rehearse this against a scratch database and record the timings before you need them.

---

## 6. Housekeeping

`SmartElderlyCare.db`, `.db-shm`, and `.db-wal` are leftovers from an earlier SQLite
prototype. They are untracked and unused — the application uses SQL Server. Delete them
so no filesystem-copy deploy can pick them up.

The `WelfareObservations` table is retained for historical records and is no longer
written to. Drop it with a separately reviewed migration once you are confident nothing
reads it.

---

## 7. Known limitations

- **DHIS2 export is CSV only.** `Dhis2MonthlyExportService` writes to the local database
  and serves a CSV download. It does not call the DHIS2 API. When a real integration is
  added, its base URL and personal access token must go straight into a secret store.
- **Enums are stored as strings**, so ordering by a status or severity column is a
  lexical sort (`'Warning' > 'Critical'`). Any report that depends on severity ordering
  needs a numeric conversion first.
- **Three foreign keys use `DeleteBehavior.NoAction`** (`FacilityVisit.VitalSignsReadingId`,
  `MonthlyReturn.SubmittedByUserId`, `MonthlyReturn.ReviewedByUserId`) and are not
  cascade-protected. Deleting a referenced row can surface as a raw SQL exception.
