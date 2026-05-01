# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
cd printer
dotnet build                    # 建置
dotnet run                      # 啟動 (http://localhost:5062)
dotnet ef migrations add <Name> # 新增 Migration
dotnet ef database update       # 套用 Migration (啟動時自動執行)
```

Database: SQL Server Express (`.\SQLEXPRESS`, database: `printer`, sa/sa)

## Architecture

ASP.NET Core 8.0 MVC + REST API 印表機雲端抄表系統。

### Core Layers

- **Controllers/** — MVC 頁面控制器 + `Api/` 子目錄為 RESTful API (`/api/v1/*`)
- **Services/** — 介面 (`I*Service`) + 實作 (`Impl/`)，透過 DI 註冊於 Program.cs
- **Data/** — `PrinterDbContext` (EF Core + SQL Server)，`Entities/` 為資料模型，`Enums/` 為列舉
- **Middleware/** — `ModuleCheckMiddleware` 根據模組啟用狀態攔截路由
- **ViewComponents/** — `ModuleMenuViewComponent` (動態選單)、`UserPermissionViewComponent`

### Module System

功能模組 (`system_modules`) 可動態啟用/停用，受 `ModuleCheckMiddleware` 保護：
- `billing` — 計費系統 (計費設定、帳單管理、計費報表)
- `einvoice` — 發票系統 (電子發票、平台設定)

路徑對應在 `ModuleCheckMiddleware.ProtectedPaths` 字典中定義。

### Permission System

角色權限 (`role_permissions`) 控制選單可見度和存取：
- 三種角色：`admin`、`supervisor`、`employee`
- 12 個功能代碼定義在 `PermissionService.Features`
- Layout 用 `@inject IPermissionService` 動態渲染選單
- Controller 層用 `[Authorize(Roles = "admin")]` 限制存取

### Authentication

Cookie 認證，密碼用 SHA256 雜湊。預設管理員：admin/admin。
Claims: NameIdentifier, Name, Username, Role。

### Database Conventions

- Table names: `snake_case` (e.g., `print_records`, `system_modules`)
- Column names: `snake_case` (e.g., `partner_id`, `is_active`)
- Entity 用 `[Table]` 和 `[Column]` attribute 明確指定
- 啟動時自動 Migrate + 種子資料 (模組、平台、權限、預設管理員)

### Key Entity Relationships

- `Partner` → `Printer[]` → `PrintRecord[]`, `AlertRecord[]`, `PrinterMaintainer[]`
- `Printer` → `PrinterBillingConfig` (one-to-one)
- `Invoice` → `InvoiceItem[]` (帳單明細)
- `Einvoice` → `EinvoiceItem[]` (發票明細)，可關聯 `Invoice` (BillingInvoiceId)
- `EinvoicePlatform` → `EinvoiceFieldMapping[]` (平台欄位對應)

### E-Invoice Platform

發票平台由程式種子預定義 (ezPay/ECPay/Tradevan)，使用者只能啟用其中一家、設定金鑰。
欄位轉換由程式碼控制，不由使用者設定。

### Frontend

- Bootstrap 5 + Tom Select (可搜尋下拉選單，class `ts-select`)
- Layout 在 `_Layout.cshtml`，Tom Select 自動初始化帶 `ts-select` class 的 select
- `@section Scripts` 用於頁面專屬 JS
- 動態表單（帳單明細、發票明細）用 JavaScript 動態新增/刪除列

### API Response Pattern

`ApiController` 使用 `ApiResponse.Success(msg, data)` / `ApiResponse.Error(msg)` 統一回傳格式。
RESTful API (`/api/v1/*`) 直接回傳 entity，需注意 JSON 循環參考已設定 `ReferenceHandler.IgnoreCycles`。

## Language

所有 UI 文字、註解、與使用者溝通使用繁體中文。技術術語和程式碼識別符維持英文。
