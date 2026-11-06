# PTL.Database scripts

Numbered SQL scripts for changes to the shared Proficiency Testing database, applied manually (no
migration tool), mirroring the equivalent `CDC.Database` convention in the D2R2/CDC repo. Each
script is run once per environment, in order, via `sqlcmd` (or SSMS "Open File" + Execute) against
that environment's connection - the same script, unedited, every time.

```powershell
sqlcmd -S <server> -d <database> -i Scripts\0001-AddSsoIdExtColumns.sql
```

## Applied-to tracker

There is no journal table, so track what has been run here:

| Script | Local | Dev | Test | Prod |
|---|---|---|---|---|
| 0001-AddSsoIdExtColumns.sql | | | | |
| 0002-ExtendParticipantProceduresForSsoIdExt.sql | | | | |
| 0003-CreateSpgParticipantByEmail.sql | | | | |
| 0004-ExtendViewerProceduresForSsoIdExt.sql | | | | |
| 0005-CreateSpgViewerByEmail.sql | | | | |
| 0006-ExtendTestConsultantProceduresForSsoIdExt.sql | | | | |
| 0007-CreateSpgTestConsultantByEmail.sql | | | | |
| 0008-AddSsoIdIntToUsers.sql | Y | | | |
| 0009-ExtendSppAuthenticateForSsoIdInt.sql | Y | | | |
| 0010-CreateSpuUserSsoIdInt.sql | Y | | | |

If this grows to the point where manually tracking applied scripts becomes error-prone, introduce
a proper migration tool (e.g. DbUp) pointed at this same `Scripts` folder rather than changing how
scripts are authored.
