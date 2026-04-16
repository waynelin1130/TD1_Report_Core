# TD1_Report_Core

`TD1_Report_Core` 為 TD1 平台的 **報表核心套件 (Report Core Library)**，負責提供報表相關的核心功能與共用邏輯。

本套件主要供 TD1 平台內部模組使用，例如：

- `TD1_Report_Api`

---

## Purpose

此專案的主要目的為：

- 集中管理 **報表共用邏輯與工具方法**
- 降低各模組間的重複開發成本
- 建立一致且可維護的報表處理架構
- 報表功能依賴 `TD1_Core` 的資料結構，僅需引用最新版本即可擴展報表能力

透過將報表相關功能集中於 `TD1_Report_Core`：

- API 模組可專注於 流程控制與商業邏輯
- 報表邏輯可統一維護與擴充
- 提升整體系統的 一致性與可維護性

---

# Release Steps

## 1. 修改版本號

編輯：

Directory.Build.props

```xml
<MajorVersion>0</MajorVersion>
<MinorVersion>0</MinorVersion>
<PatchVersion>0</PatchVersion>

<VersionSuffix>dev</VersionSuffix>
<SuffixVersion>5</SuffixVersion>
```

範例版本：

0.0.0-dev.5

---

## 2. 建置 NuGet 套件

```bash
dotnet pack -c Release
```

輸出位置：

```txt
/nupkgs
 ├─ TD1_Report_Core.x.x.x.nupkg
 └─ TD1_Report_Core.x.x.x.snupkg
```

---

## 3. 發佈到公司 NuGet

需要向其他開發人員取得 nuget_api_key

```powershell
dotnet nuget push ../nupkgs/TD1_Report_Core.x.x.x.nupkg `
-s https://nuget.tpdrg.com/v3/index.json `
-k {nuget_api_key} `
--skip-duplicate
```

---

## 4. API 專案安裝套件

```powershell
dotnet add package TD1_Report_Core --version x.x.x --source https://nuget.tpdrg.com/v3/index.json
```

---
