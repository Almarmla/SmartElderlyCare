# SmartElderlyCare

SmartElderlyCare is an ASP.NET Core MVC web application intended to support elderly-care coordination. The current interface provides a responsive login experience and a foundation for adding care dashboards, patient monitoring, appointments, and secure communication features.

## Current features

- Responsive SmartElderlyCare login page.
- Secure cookie-based administrator login.
- Protected dashboard route for authenticated users.
- EF Core entities for patients, observations, vital signs, thresholds, alerts, and monthly returns.
- Authorised patient registration with bio-data, chronic condition, and chronic care programme enrolment.
- Facility Nurse vital-sign capture linked to a registered patient.
- Structured, low-literacy VHW welfare observation capture.
- DHIO/HIU administrator threshold configuration by vital-sign metric and medical condition.
- Care-focused landing content describing:
  - Unified care overview.
  - Faster response times.
  - Secure communication.
- Email and password input fields.
- Remember-me checkbox.
- Forgot-password and account-creation placeholders.
- Google and Microsoft sign-in button placeholders.
- Fixed local development ports.
- Standard ASP.NET Core MVC error and privacy pages.

The configured administrator account is:

- Name: `Almar Mlambo`
- Email: `almarmlambo@gmail.com`

The password is stored as a salted PBKDF2 hash in `appsettings.json`, not as plain text. For production, move the admin configuration to user secrets or environment variables.

## Technology stack

- .NET 8
- ASP.NET Core MVC
- Entity Framework Core 8
- SQL Server provider for Entity Framework Core
- Bootstrap
- jQuery and jQuery Validation
- Razor views

## Project structure

```text
SmartElderlyCare/
├── Controllers/
│   └── HomeController.cs
├── Models/
│   ├── Alert.cs
│   ├── ApplicationRole.cs
│   ├── ApplicationUser.cs
│   ├── MonthlyReturn.cs
│   ├── Patient.cs
│   ├── Threshold.cs
│   ├── VitalSignsReading.cs
│   ├── WelfareObservation.cs
│   └── ErrorViewModel.cs
├── Data/
│   └── ApplicationDbContext.cs
├── Properties/
│   └── launchSettings.json
├── Views/
│   ├── Home/
│   │   ├── Index.cshtml
│   │   ├── Login.cshtml
│   │   └── Privacy.cshtml
│   ├── Shared/
│   │   ├── Error.cshtml
│   │   ├── _Layout.cshtml
│   │   └── _ValidationScriptsPartial.cshtml
│   ├── _ViewImports.cshtml
│   └── _ViewStart.cshtml
├── wwwroot/
│   ├── css/
│   │   └── site.css
│   ├── js/
│   │   └── site.js
│   └── lib/
├── appsettings.Development.json
├── appsettings.json
├── Program.cs
├── SmartElderlyCare.slnx
└── SmartElderlyCare.csproj
```

## Data model

The EF Core model is defined in `Data/ApplicationDbContext.cs` and includes:

- `Patient` - resident demographic and care assignment information.
- `ApplicationUser` - ASP.NET Core Identity user with support for the `Nurse`, `FacilityNurse`, `VHW`, `Family`, `RecordsStaff`, `HiuClerk`, `DhioAdmin`, and `Dmo` roles.
- `FacilityVisit` - a facility nurse visit containing captured vital signs, diagnosis, treatment, clinical notes, and visit status.
- `VhwWelfareCheck` - a simplified welfare observation for Village Health Workers, including mood, mobility, nutrition, safety concerns, notes, and follow-up.
- `PatientFamilyMember` - an optional link between a patient and a registered family member with viewer or contributor access.
- `MonthlyReturnValidation` - an auditable validation decision made by an HIU Data Clerk.
- `Dhis2IndicatorSubmission` - an auditable DHIS2 indicator capture and submission record.
- `AdministrationAuditLog` - an audit trail for DHIO/HIU administrator user-account and threshold changes.
- `DistrictReport` - a published district-level aggregate report for DMO consumption.
- `AlertService` - compares saved vital-sign readings with active condition-specific thresholds and creates alerts for out-of-range values.
- `Dhis2MonthlyExportService` - aggregates monthly readings and welfare observations into DHIS2-style CSV indicators using CsvHelper.
- `VitalSignsReading` - recorded temperature, blood pressure, pulse, respiratory rate, oxygen saturation, and weight.
- `WelfareObservation` - wellbeing, mood, mobility, nutrition, hydration, and social engagement observations.
- `Threshold` - configurable vital-sign boundaries used to trigger alerts.
- `Alert` - patient alerts with severity, status, assignment, and resolution tracking.
- `MonthlyReturn` - monthly reporting and submission information for each patient.

The context configures foreign keys, delete behavior, enum storage, decimal precision, unique patient numbers, and one monthly return per patient/month.

### Facility Nurse workflow

The `FacilityNurse` role is intended for nurses working at a care facility. A facility nurse records:

1. The patient attending the facility visit.
2. Vital signs, stored in `VitalSignsReading`.
3. The clinical diagnosis.
4. Treatment or care provided.
5. Optional clinical notes and the visit status.

`FacilityVisit` links the visit to the nurse account, patient, and optional vital-sign reading so the full clinical record can be audited.

### Patient registration

Authorised users can use `/Patients/Register` to register an elderly patient with:

1. Patient number, name, date of birth, sex, national ID, phone, and address.
2. Chronic medical condition and chronic care programme.
3. Emergency contact details.

Registered patients can be reviewed at `/Patients`. Patient numbers are unique and future dates of birth are rejected.
The patient registry supports searching by patient number, name, or condition. Selecting `History` retrieves the patient's vital readings, welfare captures, facility visits, and alerts for a chosen date range.

### Role workspaces and dashboard communication

After sign-in, the main dashboard links to `/Workspace/Index` through **My workspace** and **Open my workspace**. The workspace resolves the signed-in role and presents only the relevant next actions:

- `Nurse` / `FacilityNurse`: record a facility visit, find a patient, or register a patient.
- `VHW`: submit a welfare check or find a patient.
- `HiuClerk` / `RecordsStaff`: open the HIU dashboard, download the monthly DHIS2 report, or find a patient.
- `DhioAdmin`: manage users, configure thresholds, or review patient history.
- `Family`: review available patient history.
- `Dmo`: open the monthly district reporting export.

Every action card links directly to its MVC controller or Razor Page, so the dashboard acts as the navigation point between role responsibilities.

### Village Health Worker workflow

The `VHW` role uses `VhwWelfareCheck` as a simplified observation form for community visits:

1. Select the patient.
2. Record the patient's mood, mobility, nutrition, and safety status.
3. Add optional notes.
4. Mark whether follow-up is required and provide a follow-up date.

The check is linked to the VHW account and patient. Urgent or follow-up observations can be used to create an `Alert` for the clinical team.

The simplified VHW Razor Page is available at `/Vhw/WelfareCheck` and uses large Bootstrap controls for patient, mobility, medication, general condition, and notes.

### Threshold administration

Users in the `DhioAdmin` role can use the Administration area to create or edit active safe ranges at `/Admin/Thresholds/Edit`. Each threshold records a metric, optional medical condition, minimum and maximum values, unit, severity, and active state. Saved ranges are used by `AlertService` after vital-sign capture.

### Threshold alerting

`AlertService.CheckThresholds` runs after a facility nurse saves a vital-sign reading. It selects active thresholds matching the patient's medical condition, or thresholds with no condition specified, compares every configured metric, and persists an `Alert` for each out-of-range value.

### Historical trends and DHIS2 monthly reporting

The patient history page displays readings in the selected period in chronological order for trend review, together with the complete capture history. HIU clerks, Records Staff, and DHIO administrators can download a DHIS2-formatted monthly CSV at `/Hiu/MonthlyReport`.

## HIU dashboard and DHIS2 export

The HIU Clerk dashboard is available at `/Hiu/Dashboard` and is protected by the `HiuClerk` role. It shows recent vital-sign readings and active alerts sorted by severity. The `Mark reviewed` button sends an AJAX POST and records `Reviewed`, `ReviewedAt`, and `ReviewedByUserId` on the alert.

`Dhis2MonthlyExportService.ExportMonthAsCsvAsync` aggregates a selected month into DHIS2-style rows with:

- `DataElement`
- `Period` in `yyyyMM` format
- `OrganisationUnit`
- `Value`

The export includes vital-sign readings, elevated blood-pressure readings, welfare observations, urgent welfare observations, and welfare observations needing attention.

### Registered family member access

The `Family` role is optional in v1. A patient does not need a registered family member to use the system.

When a family member is registered, `PatientFamilyMember` can grant:

- `Viewer` access for viewing permitted care information.
- `Contributor` access for adding permitted updates or notes.

The relationship is inactive-capable and uses a composite key of patient and family user, preventing duplicate links while allowing access to be revoked without deleting the audit relationship.

### Records Staff workflow

The `RecordsStaff` role compiles and reviews monthly returns before they are sent for HIU validation. Review details are stored on `MonthlyReturn`, including the reviewing user, review time, and notes.

### HIU Data Clerk workflow

The `HiuClerk` role:

1. Validates or rejects compiled monthly returns.
2. Records the validation decision and notes in `MonthlyReturnValidation`.
3. Captures monthly indicators for DHIS2 in `Dhis2IndicatorSubmission`.
4. Tracks each submission as pending, submitted, or failed, including the DHIS2 response or error.
5. Uses the existing `Alert` entity to monitor patient and care-team alerts.

### DHIO / HIU Administrator workflow

The `DhioAdmin` role manages the system after handover. Administrator actions include:

- Creating, updating, disabling, and changing roles for user accounts.
- Creating, updating, and disabling clinical thresholds.
- Recording every administrative action in `AdministrationAuditLog`.

### District Medical Officer workflow

The `Dmo` role consumes published `DistrictReport` records only. District reports contain aggregate indicators such as patient counts, completed visits, welfare checks, and alert totals. They do not grant DMO access to edit users, thresholds, patient records, or operational submissions.

## DHIO / HIU admin area

The Identity-protected admin area is available at:

- `/Admin`
- `/Admin/Users`
- `/Admin/Thresholds/Edit`

Only users in the `DhioAdmin` role can access these pages. Administrators can create, edit, reactivate, and deactivate accounts, assign seeded roles, and create or edit condition-specific clinical thresholds. Changes are recorded in `AdministrationAuditLog`.

## Prerequisites

Install the following before running the project:

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A supported code editor, such as [Visual Studio](https://visualstudio.microsoft.com/) or [Visual Studio Code](https://code.visualstudio.com/)

SQL Server is included as an Entity Framework Core dependency, but no database connection is currently required to display the login page.

Identity role seeding and the facility nurse data-capture workflow require a reachable SQL Server instance. The default development connection targets:

```text
(localdb)\MSSQLLocalDB
```

If LocalDB is not installed, update `ConnectionStrings:DefaultConnection` in `appsettings.json` to point to an available SQL Server. The web application logs a warning and continues starting when the database is unavailable, but role seeding and database writes will remain unavailable until the connection is fixed.

## Run the application

Open a terminal in the project directory:

```powershell
cd "C:\Users\HomePC\Desktop\Almar Huro Project\SmartElderlyCare"
dotnet restore
dotnet run --launch-profile https
```

When opening the project in VS Code with C# Dev Kit, open `SmartElderlyCare.slnx`. It is the checked-in solution file and prevents the editor from relying on a generated workspace solution.

Then open the single application endpoint:

- [https://localhost:7159](https://localhost:7159)

The root route redirects to the login page:

```text
/
└── /Home/Login
```

After successful administrator login, the application opens:

```text
/Home/Dashboard
```

## Role-based authentication

ASP.NET Core Identity is configured with these roles:

- `Nurse`
- `VHW`
- `Family`
- `HiuClerk`
- `DhioAdmin`
- `Dmo`

The roles are seeded at startup when the configured database is available. The facility visit endpoint demonstrates role protection with `[Authorize(Roles = "Nurse")]` at `/FacilityNurse/RecordVisit`.

On the first HTTPS run, the local development certificate may require browser approval. If the certificate is not trusted, run:

```powershell
dotnet dev-certs https --trust
```

## Build the application

```powershell
dotnet build
```

## Configuration

The fixed local URL is configured in both:

- `Properties/launchSettings.json`
- `appsettings.json`

Current URLs:

```text
https://localhost:7159
```

If the port is already in use, change the URL in both files so the launch profile and direct application startup remain consistent.

## Next development steps

Recommended next steps for turning the UI into a complete application:

1. Add ASP.NET Core Identity or another authentication provider.
2. Connect the login form to a POST action or identity endpoint.
3. Add server-side model validation and authentication error messages.
4. Replace the social sign-in placeholders with OAuth/OpenID Connect providers.
5. Add an authenticated care dashboard.
6. Configure and apply Entity Framework Core database migrations.
7. Add automated tests for authentication and protected routes.

## License

No project license has been specified yet.
