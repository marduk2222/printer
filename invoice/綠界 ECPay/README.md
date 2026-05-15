---
title: 綠界 ECPay 電子發票 API 文件彙整
source_root: https://developers.ecpay.com.tw/
fetched: 2026-05-09
---

# 綠界 ECPay 電子發票 API 文件

本資料夾彙整綠界科技 (ECPay) 三套電子發票 API 的開發者文件，從 https://developers.ecpay.com.tw/ 抓取並轉為 markdown 保存，供本專案開發發票模組時離線參考。

> **注意**：所有 md 內容由 WebFetch 經 LLM 取得，已盡量保留欄位表、endpoint、Request/Response 範例，但仍可能與官方原文有細微差異。**正式介接時請以綠界官方頁面為準**。

## 三套 API 的差異

| 系統 | 對象 | 適用場景 | 加密方式 | 文件夾 |
|---|---|---|---|---|
| **B2C 電子發票** | 一般消費者 | 線上店家、雲端 POS 介接，可即時/延遲/預約開立 | AES-128-CBC + SHA256 檢查碼 | [`b2c/`](./b2c/) |
| **B2B 電子發票** | 公司行號（含買賣雙方） | 交換模式（存證+交換）、需確認流程 | AES + 檢查碼 | [`b2b/`](./b2b/) |
| **離線電子發票** | 已有實體發票機台的特店 | 字軌配號、機台管理、上傳離線開立發票 | AES + URLEncode | [`offline/`](./offline/) |

## API Endpoint 速查

### B2C（base：`https://einvoice-stage.ecpay.com.tw/` 測試 / `https://einvoice.ecpay.com.tw/` 正式）

| 動作 | 路徑 | 文件 |
|---|---|---|
| 一般開立 | `POST /B2CInvoice/Issue` | [b2c/14](./b2c/14-一般開立發票.md) |
| 延遲開立（預約） | `POST /B2CInvoice/DelayIssue` | [b2c/15](./b2c/15-延遲開立發票-預約開立.md) |
| 編輯延遲開立 | `POST /B2CInvoice/UpdateDelayIssue` | [b2c/16](./b2c/16-編輯延遲開立發票.md) |
| 觸發開立 | `POST /B2CInvoice/TriggerIssue` | [b2c/17](./b2c/17-觸發開立發票.md) |
| 取消延遲開立 | `POST /B2CInvoice/CancelDelayIssue` | [b2c/18](./b2c/18-取消延遲開立發票.md) |
| 一般折讓（紙本） | `POST /B2CInvoice/Allowance` | [b2c/19](./b2c/19-一般開立折讓-紙本.md) |
| 線上折讓（通知） | `POST /B2CInvoice/AllowanceByCollegiate` | [b2c/20](./b2c/20-線上開立折讓-通知開立.md) |
| 作廢發票 | `POST /B2CInvoice/Invalid` | [b2c/21](./b2c/21-作廢發票.md) |
| 作廢折讓 | `POST /B2CInvoice/AllowanceInvalid` | [b2c/22](./b2c/22-作廢折讓.md) |
| 取消線上折讓 | `POST /B2CInvoice/CancelAllowanceByCollegiate` | [b2c/23](./b2c/23-取消線上折讓.md) |
| 註銷重開 | `POST /B2CInvoice/IssueWithCancel` | [b2c/24](./b2c/24-註銷重開.md) |
| 查詢發票 | `POST /B2CInvoice/GetIssue` | [b2c/25](./b2c/25-查詢發票明細.md) |
| 查詢多筆 | `POST /B2CInvoice/GetIssueList` | [b2c/26](./b2c/26-查詢特定多筆發票.md) |
| 查詢折讓 | `POST /B2CInvoice/GetAllowance` | [b2c/27](./b2c/27-查詢折讓明細.md) |
| 查詢作廢 | `POST /B2CInvoice/GetInvalid` | [b2c/28](./b2c/28-查詢作廢發票明細.md) |
| 查詢作廢折讓 | `POST /B2CInvoice/GetAllowanceInvalid` | [b2c/29](./b2c/29-查詢作廢折讓明細.md) |
| 重發通知 | `POST /B2CInvoice/InvoiceNotify` | [b2c/30](./b2c/30-發送發票通知.md) |
| 列印 | `POST /B2CInvoice/InvoicePrint` 等 | [b2c/31](./b2c/31-發票列印.md) |

### B2B（base：`https://einvoice-stage.ecpay.com.tw/` 測試 / `https://einvoice.ecpay.com.tw/` 正式）

| 動作 | 路徑 | 文件 |
|---|---|---|
| 開立 | `POST /B2BInvoice/Issue` | [b2b/12](./b2b/12-開立發票.md) |
| 開立確認 | `POST /B2BInvoice/IssueConfirm` | [b2b/13](./b2b/13-開立發票確認.md) |
| 作廢 | `POST /B2BInvoice/Invalid` | [b2b/14](./b2b/14-作廢發票.md) |
| 作廢確認 | `POST /B2BInvoice/InvalidConfirm` | [b2b/15](./b2b/15-作廢發票確認.md) |
| 退回 | `POST /B2BInvoice/Reject` | [b2b/16](./b2b/16-退回發票.md) |
| 退回確認 | `POST /B2BInvoice/RejectConfirm` | [b2b/17](./b2b/17-退回發票確認.md) |
| 折讓 | `POST /B2BInvoice/Allowance` | [b2b/18](./b2b/18-開立折讓發票.md) |
| 折讓確認 | `POST /B2BInvoice/AllowanceConfirm` | [b2b/19](./b2b/19-折讓發票確認.md) |
| 作廢折讓 | `POST /B2BInvoice/AllowanceInvalid` | [b2b/20](./b2b/20-作廢折讓發票.md) |
| 作廢折讓確認 | `POST /B2BInvoice/AllowanceInvalidConfirm` | [b2b/21](./b2b/21-作廢折讓發票確認.md) |
| 查詢系列 | `POST /B2BInvoice/Get*` | [b2b/22-31](./b2b/) |

### 離線（base：`https://einvoice-stage.ecpay.com.tw/` 測試 / `https://einvoice.ecpay.com.tw/` 正式）

| 動作 | 路徑 | 文件 |
|---|---|---|
| 查特店資料 | `POST /OfflineInvoice/MerchantInfo` | [offline/07](./offline/07-查詢特店基本資料.md) |
| 查財政部配號 | `POST /OfflineInvoice/GovTrackResult` | [offline/08](./offline/08-查詢財政部配號結果.md) |
| 機台管理 | `POST /OfflineInvoice/MachineInfo` | [offline/09](./offline/09-管理發票機台.md) |
| 字軌配號 | `POST /OfflineInvoice/InvoiceNumberSetting` | [offline/10](./offline/10-字軌與配號設定.md) |
| 字軌狀態設定 | `POST /OfflineInvoice/InvoiceNumberStatus` | [offline/11](./offline/11-設定字軌號碼狀態.md) |
| 字軌區間 | `POST /OfflineInvoice/InvoiceNumberRange` | [offline/13](./offline/13-取得發票字軌號碼區間.md) |
| 字軌清單（含隨機碼） | `POST /OfflineInvoice/InvoiceNumberListWithRand` | [offline/14](./offline/14-取得發票字軌號碼清單.md) |
| 上傳開立 | `POST /OfflineInvoice/Upload` | [offline/15](./offline/15-上傳開立發票.md) |
| 上傳作廢 | `POST /OfflineInvoice/UploadInvalid` | [offline/16](./offline/16-上傳作廢發票.md) |
| 查機台 | `POST /OfflineInvoice/GetMachineInfo` | [offline/17](./offline/17-查詢發票機台.md) |
| 查字軌 | `POST /OfflineInvoice/GetInvoiceNumber` | [offline/18](./offline/18-查詢字軌.md) |

> 表中路徑以各文件實際 endpoint 為準；少數頁面（如折讓開立）路徑可能因綠界版本更新而調整，請對照各 md 內 *API 介接網址* 章節。

## 認證憑證

三套 API 共用同一組「特店 (Merchant)」憑證：

- **MerchantID**：特店編號（10 碼）
- **HashKey**：AES 對稱金鑰（用於加密 `Data`）
- **HashIV**：AES 初始向量
- **PlatformID**（選填）：平台商代號

> 對應到本專案 `EinvoicePlatform` 實體：MerchantID → `MerchantId`、HashKey → `ApiKey`、HashIV → `ApiSecret`。

## 加密流程（B2C / B2B 共通）

1. 將 Request 內 `Data` 區塊組成 JSON 字串
2. URLEncode（`%` 編碼，依 [URLEncode 轉換表](./b2c/35-URLEncode轉換表.md)）
3. AES-128-CBC 加密（PKCS7 padding，HashKey 作為 Key、HashIV 作為 IV）
4. Base64 編碼
5. 將結果放回 Request `Data` 欄位

回傳資料反向：Base64 解碼 → AES 解密 → URLDecode → 取得 JSON。

詳細請參考：
- [B2C 加密說明](./b2c/33-參數加密方式說明.md)
- [B2C 檢查碼機制](./b2c/34-檢查碼機制說明.md)
- [B2B 加密說明](./b2b/36-參數加密方式說明.md)
- [離線 加密說明](./offline/21-參數加密方式說明.md)

## 章節對照表

### 離線電子發票（offline/，22 頁）
| # | 標題 | 來源 |
|---|---|---|
| 01 | 簡介 | /13738/ |
| 02 | 重要詞彙說明 | /13752/ |
| 03 | 更新歷程 | /58889/ |
| 04 | 使用流程圖說明 | /13758/ |
| 05 | 測試介接資訊 | /13763/ |
| 06 | 介接注意事項 | /13768/ |
| 07 | 查詢特店基本資料 | /13773/ |
| 08 | 查詢財政部配號結果 | /13778/ |
| 09 | 管理發票機台 | /13783/ |
| 10 | 字軌與配號設定 | /13788/ |
| 11 | 設定字軌號碼狀態 | /13793/ |
| 12 | 發送發票通知 | /45974/ |
| 13 | 取得發票字軌號碼區間 | /13795/ |
| 14 | 取得發票字軌號碼清單 | /15502/ |
| 15 | 上傳開立發票 | /13823/ |
| 16 | 上傳作廢發票 | /13828/ |
| 17 | 查詢發票機台 | /13833/ |
| 18 | 查詢字軌 | /13843/ |
| 19 | 錯誤代碼查詢 | /13853/ |
| 20 | URLEncode 轉換表 | /13858/ |
| 21 | 參數加密方式說明 | /13863/ |
| 22 | 電子發票列印格式說明 | /31732/ |

### B2C 電子發票（b2c/，35 頁）
| # | 標題 | 來源 |
|---|---|---|
| 01 | 簡介 | /7809/ |
| 02 | 重要詞彙說明 | /7824/ |
| 03 | 更新歷程 | /36050/ |
| 04 | 使用流程圖說明 | /7829/ |
| 05 | 測試介接資訊 | /7849/ |
| 06 | 介接注意事項 | /7854/ |
| 07 | 查詢財政部配號結果 | /7859/ |
| 08 | 字軌與配號設定 | /7870/ |
| 09 | 設定字軌號碼狀態 | /7875/ |
| 10 | 查詢字軌 | /7881/ |
| 11 | 統一編號驗證 | /32089/ |
| 12 | 手機條碼驗證 | /7886/ |
| 13 | 捐贈碼驗證 | /7891/ |
| 14 | 一般開立發票 | /7896/ |
| 15 | 延遲開立發票（預約） | /?p=15369 |
| 16 | 編輯延遲開立發票 | /47979/ |
| 17 | 觸發開立發票 | /?p=15371 |
| 18 | 取消延遲開立發票 | /?p=15382 |
| 19 | 一般開立折讓（紙本） | /7901/ |
| 20 | 線上開立折讓（通知） | /15391/ |
| 21 | 作廢發票 | /7906/ |
| 22 | 作廢折讓 | /7911/ |
| 23 | 取消線上折讓 | /7913/ |
| 24 | 註銷重開 | /7918/ |
| 25 | 查詢發票明細 | /7923/ |
| 26 | 查詢特定多筆發票 | /?p=17229 |
| 27 | 查詢折讓明細 | /7928/ |
| 28 | 查詢作廢發票明細 | /7933/ |
| 29 | 查詢作廢折讓明細 | /7943/ |
| 30 | 發送發票通知 | /7938/ |
| 31 | 發票列印 | /7949/ |
| 32 | 錯誤代碼查詢 | /7954/ |
| 33 | 參數加密方式說明 | /7958/ |
| 34 | 檢查碼機制說明 | /38242/ |
| 35 | URLEncode 轉換表 | /38406/ |

### B2B 電子發票（b2b/，36 頁）
| # | 標題 | 來源 |
|---|---|---|
| 01 | 簡介 | /14808/ |
| 02 | 更新歷程 | /36064/ |
| 03 | 使用流程圖說明 | /25224/ |
| 04 | 準備事項 | /14815/ |
| 05 | 測試介接資訊 | /14820/ |
| 06 | 介接注意事項 | /14825/ |
| 07 | 交易對象維護 | /14830/ |
| 08 | 查詢財政部配號結果 | /25206/ |
| 09 | 字軌與配號設定 | /14835/ |
| 10 | 設定字軌號碼狀態 | /14840/ |
| 11 | 查詢字軌 | /14845/ |
| 12 | 開立發票 | /14850/ |
| 13 | 開立發票確認 | /14855/ |
| 14 | 作廢發票 | /14860/ |
| 15 | 作廢發票確認 | /14865/ |
| 16 | 退回發票 | /14870/ |
| 17 | 退回發票確認 | /14875/ |
| 18 | 開立折讓發票 | /14923/ |
| 19 | 折讓發票確認 | /14880/ |
| 20 | 作廢折讓發票 | /14889/ |
| 21 | 作廢折讓發票確認 | /14894/ |
| 22 | 查詢發票 | /14935/ |
| 23 | 查詢發票確認 | /14940/ |
| 24 | 查詢作廢發票 | /14948/ |
| 25 | 查詢作廢發票確認 | /14953/ |
| 26 | 查詢退回發票 | /14958/ |
| 27 | 查詢退回發票確認 | /14963/ |
| 28 | 查詢折讓發票 | /14968/ |
| 29 | 查詢折讓發票確認 | /14973/ |
| 30 | 查詢作廢折讓發票 | /14978/ |
| 31 | 查詢作廢折讓發票確認 | /14983/ |
| 32 | 發送發票通知 | /14988/ |
| 33 | 發票列印 | /14993/ |
| 34 | 發票列印 PDF | /53383/ |
| 35 | 交易訊息代碼一覽表 | /14998/ |
| 36 | 參數加密方式說明 | /15008/ |

## 其他發票廠商

- 關網 Gateweb：[`../gateweb/`](../gateweb/)（已存在的 HackMD 抓取版）
- ezPay 藍新：尚未抓取（請參考 https://inv.pay2go.com/ 商店後台技術文件）

## 已知抓取限制

- WebFetch 經 LLM 處理，並非逐字 HTML 鏡像；多數 endpoint/欄位/範例完整保留，但個別措辭與排版為改寫
- 部分頁面為導覽性質或受版權文字限制，標 `status: partial`：
  - **離線**：03 更新歷程、19 錯誤代碼（需登入後台）
  - **B2C**：01 簡介、02 詞彙、03 更新歷程、04 流程圖、32 錯誤代碼（需登入後台）
  - **B2B**：01、02、03、04、07、23、24、25、29、33、35、36 多為導覽/圖片頁
- 完整錯誤代碼表須登入綠界廠商後台查詢

## 重抓建議

若需更新，可使用 WebFetch 重抓單一頁面，或重新派發子代理執行 `下載 N 頁文件` 工作。各檔 frontmatter 已含 `source` URL 可直接對照官方頁面。
