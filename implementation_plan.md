# Route Audit & UI Fix Plan

## Route Audit Results ✅

All sidebar buttons in [AppShell.xaml.cs](file:///c:/Coding%20Files/C%23/MawasaSystem/MawasaProject/MawasaProject.Presentation/Shell/AppShell.xaml.cs) map correctly to [RouteMap.cs](file:///c:/Coding%20Files/C%23/MawasaSystem/MawasaProject/MawasaProject.Presentation/Services/Navigation/RouteMap.cs) routes:

| Sidebar Button | Route | FlyoutItem | Status |
|---|---|---|---|
| Dashboard | `//dashboard/home` | `DashboardItem` | ✅ OK |
| Billing | `//billing/home` | `BillingItem` | ✅ OK |
| Payments | `//payments/home` | `PaymentsItem` | ✅ OK |
| Customers | `//customers/home?mode=manage` | `CustomersItem` | ✅ OK |
| Customer Management | `//customers/home?mode=manage` | sub-item | ✅ OK |
| Create/Register Customers | `//customers/home?mode=register` | sub-item | ✅ OK |
| Reports | `//reports/home?mode=payments` | `ReportsItem` | ✅ OK |
| Customer Payment Report | `//reports/home?mode=payments` | sub-item | ✅ OK |
| Issue Report | `//reports/home?mode=issues` | sub-item | ✅ OK |
| Print Reports | `//reports/home?mode=print` | sub-item | ✅ OK |
| Audit | `//audit/home` | `AuditItem` | ✅ OK |
| Settings | `//settings/home` | `SettingsItem` | ✅ OK |
| Backup (Settings sub) | `backup` | registered route | ✅ OK |
| Printer Settings | `printer-settings` | registered route | ✅ OK |
| Print Queue | `print-queue` | registered route | ✅ OK |

**No broken routes found.**

---

## UI Issues Found & Proposed Fixes

### 1. Login Page
- **Issue:** The card background is a flat light-gray `#F4F7FB`, looks too plain. The subtitle has a negative margin that overlaps poorly. The checkbox is misaligned (`–` symbol showing due to CheckBox rendering on Windows).
- **Fixes:**
  - Change card background to pure `#FFFFFF` with a very soft shadow
  - Increase title size contrast; add a separator line below the subtitle
  - Fix "Remember me" row — use correct column span so checkbox and "Forgot password?" don't overlap
  - Remove negative margin on subtitle label

### 2. Settings Page
- **Issue:** The Settings page is completely unstyled — plain white, default-blue buttons. Looks like a placeholder.
- **Fix:** Restyle [SettingsPage.xaml](file:///c:/Coding%20Files/C%23/MawasaSystem/MawasaProject/MawasaProject.Presentation/Views/Pages/SettingsPage.xaml) to match the card-based design system used by other pages (white cards, rounded borders, consistent colors).

### 3. Dashboard Page  
- **Issue:** Contains a dead, hidden old sidebar (`Grid`, `IsVisible="False"`, `Width="0"`) that should be removed since `AppShell.FlyoutContent` is the real sidebar now.
- **Issue:** Header shows hardcoded `"Administrator"` and `"Admin User"` labels.
- **Fix:** Remove the ghost sidebar Grid. Update the header to bind to session data (username from `AppStateStore`).

---

## Proposed Changes

### Login Page
#### [MODIFY] [LoginPage.xaml](file:///c:/Coding%20Files/C%23/MawasaSystem/MawasaProject/MawasaProject.Presentation/Views/Pages/LoginPage.xaml)
- Fix card background color to `#FFFFFF`
- Remove negative margin on subtitle, add a thin divider
- Fix Remember Me / Forgot Password row alignment
- Remove `Margin="0,-8,0,6"` from subtitle label

### Settings Page
#### [MODIFY] [SettingsPage.xaml](file:///c:/Coding%20Files/C%23/MawasaSystem/MawasaProject/MawasaProject.Presentation/Views/Pages/SettingsPage.xaml)
- Apply the full card-based design (background `#E9EDF3`, white border cards, rounded buttons with icons)
- Add section header and descriptive text for each button
- Style the Logout button in red

### Dashboard Page
#### [MODIFY] [DashboardPage.xaml](file:///c:/Coding%20Files/C%23/MawasaSystem/MawasaProject/MawasaProject.Presentation/Views/Pages/DashboardPage.xaml)
- Remove the dead hidden old sidebar `Grid` (lines 21-71)
- Fix column definition to single-column since the sidebar is now in AppShell
- Connect username/role display to session data

---

## Verification Plan

### Manual Verification
1. Run the app: `dotnet run --project MawasaProject/MawasaProject.Presentation/MawasaProject.Presentation.csproj`
2. **Login:** Verify the login card looks clean and professional, checkbox and forgot password are properly aligned
3. **Sidebar nav:** Press each button and confirm no crash and correct page loads
4. **Settings:** Verify the page now has a styled card-based UI
5. **Dashboard:** Verify no duplicate layout artifacts, and header shows real user info
