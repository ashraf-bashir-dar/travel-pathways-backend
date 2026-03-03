# Employee Management – In-App, Per-Tenant Design

This document describes how **all employee-related activities** are handled inside the application so travel agencies can stop using offline tools (Excel, paper, etc.), and how you **assign or not assign** this functionality per tenant.

---

## 1. Assigning the feature to a tenant

- **Super Admin** controls which tenants (travel agencies) get Employee Management.
- In **Agents** (Travel Agents), when creating or editing a tenant, the **Modules** section includes **Employee Management**.
  - **Checked** → The tenant sees the Employee Management menu and all sub-features (TimeSheet, Compensation, Packages, Attendance, Salary, Details). All related APIs are allowed for that tenant.
  - **Unchecked** → The tenant does not see the menu and gets **403** from any employee-related API (tasks, compensation, confirmed packages, etc.).
- Backend stores this in **`Tenant.EnabledModules`**. If the list is null/empty, the app treats it as “all modules allowed” for backward compatibility. Once you set `EnabledModules`, only the listed modules are allowed for that tenant.

So: **you can assign or not assign Employee Management to any tenant** by toggling the Employee Management module for that tenant in the Super Admin tenant form.

---

## 2. TimeSheet as a separate module

- **TimeSheet** is its own module (`AppModuleKey.TimeSheet`). You can give users **only TimeSheet** (daily tasks: “add what you did for the day”) without giving them full Employee Management (Compensation, Packages, Attendance, etc.).
- **Super Admin:** In Agents → [tenant] → Modules, you can enable **TimeSheet** and/or **Employee Management** independently.
- **Tenant Admin:** In Users → [user] → Modules, you can assign **TimeSheet** only, or **Employee Management** only, or both.
- The daily-tasks API allows a tenant that has **either** TimeSheet **or** Employee Management.

## 3. User-level access (within a tenant)

- When a tenant has a module enabled (e.g. TimeSheet or Employee Management), the **tenant admin** can control which **users** see it via **Allowed Modules**.
- If a user’s allowed modules list is **empty**, they get all modules that the tenant has. If the list is set, they only get the listed modules.
- So: enable **TimeSheet** for the tenant, then assign **TimeSheet** only to users who should just log daily tasks; assign **Employee Management** to users who need compensation, packages, etc.

---

## 4. What is under “Employee Management”

Everything below is gated by the **Employee Management** module (tenant must have it enabled; user must have it in allowed modules if the tenant uses restricted modules).

| Feature | Route / API | Status | Description |
|--------|------------|--------|-------------|
| **TimeSheet** | `/timesheet`, `api/employee-monitoring` | ✅ Implemented | Daily tasks: users add tasks; tenant admin views all (read-only). |
| **Daily Tasks** (hub link) | `/employee-management/daily-tasks` | ✅ Implemented | Same as TimeSheet; alternate entry from hub. |
| **Employee Compensation** | `/employee-management/compensation`, `api/tenant/compensation` | ✅ Implemented | Salary, incentive, bonus records per employee. |
| **Employee Packages** | `/employee-management/packages`, `api/tenant/reports/confirmed-packages` | ✅ Implemented | Confirmed packages by employee. |
| **Employee Attendance** | `/employee-management/attendance` | 🔜 Placeholder | Attendance and leave – to be implemented in-app. |
| **Employee Salary** | `/employee-management/salary` | 🔜 Placeholder | Salary structure and payroll – to be implemented in-app. |
| **Employee Details** | `/employee-management/details` | 🔜 Placeholder | Profile and employment details – to be implemented in-app. |

All of the above are **in-app only** when the tenant has the module: no dependency on offline work for these areas.

---

## 5. Backend enforcement (no offline bypass)

- **Employee tasks (TimeSheet):** `EmployeeMonitoringController` – every action calls `EnsureTimeSheetOrEmployeeManagementModuleAsync()`. If the tenant has neither TimeSheet nor Employee Management → **403**.
- **Compensation:** `EmployeeCompensationController` – every action calls `EnsureEmployeeManagementModuleAsync()` → **403** if module not enabled.
- **Confirmed packages (employee packages):** `TenantReportsController.GetConfirmedPackages` – uses `EnsureEmployeeManagementModuleAsync()` → **403** if module not enabled.

So even if someone calls the API directly, they cannot use employee features for a tenant that does not have Employee Management enabled.

---

## 6. Adding new employee features (keep everything in-app and assignable)

To add a new employee-related feature and keep the “all in-app, assignable per tenant” design:

1. **Backend**
   - Add the new API under a controller that either:
     - Uses the same `EnsureEmployeeManagementModuleAsync()` pattern (e.g. in a new or existing controller that has access to `_db` and `_tenant`), or
     - Lives under a route that already runs this check.
   - Return **403** when the tenant does not have **Employee Management** (or legacy **EmployeeMonitoring**) in `Tenant.EnabledModules` (when that list is non-empty).

2. **Frontend**
   - Add the new feature under the **Employee Management** hub (e.g. new item in `employee-management-hub.component` with route under `/employee-management/...`).
   - Use **`moduleKey: AppModuleKey.EmployeeManagement`** and **`moduleAccessGuard`** on the route so the feature is only accessible when the tenant and user have the module.
   - No new module key is required unless you explicitly want a separate switch; keeping everything under **EmployeeManagement** keeps a single “assign or not assign” switch per tenant.

3. **Database**
   - Add any new tables/columns and document them (e.g. in `Data/Migrations` or `docs`). Include `TenantId` (and soft-delete if you use it) so data stays tenant-scoped.

4. **Docs**
   - Update this file (or your main product docs) so the new feature is listed under “What is under Employee Management” and any new APIs are documented.

This way, every new employee activity remains in-app and controlled by the same per-tenant (and per-user) assignment.

---

## 7. Summary

- **All employee-related activities** are designed to be handled in the application (TimeSheet, Compensation, Employee Packages; placeholders for Attendance, Salary, Details).
- **TimeSheet only:** You can enable **TimeSheet** per tenant and assign it to users so they only get daily tasks (add what they did for the day), without Compensation, Packages, or other Employee Management features.
- **Assign per tenant:** Super Admin enables **TimeSheet** and/or **Employee Management** per tenant in Agents → [tenant] → Modules.
- **User-level:** Tenant admin assigns **TimeSheet** and/or **Employee Management** to users via **Allowed Modules**.
- **No offline dependency:** Backend enforces the module on every employee API so agencies can run fully in-app when you enable the module for them.
