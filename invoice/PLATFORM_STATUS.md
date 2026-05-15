---
title: 發票平台串接狀況
updated: 2026-05-14
---

# 發票平台串接狀況

三家發票平台串接的最後驗證結果（沙盒實測為主）。

## 總覽

| 平台 (Code) | 開立 | 作廢 | 開立折讓 | 作廢折讓 | 沙盒實測 | 備註 |
|---|---|---|---|---|---|---|
| **綠界 ECPay** (`ecpay`) | ✅ | ✅ | ✅ | ✅ | 已通過 | 依買方統編自動 dispatch B2C/B2B |
| **藍新 ezPay** (`ezpay`) | ✅ | ✅ | ✅ | ✅ | 已通過 | B2C/B2B 共用 endpoint，靠 `Category` 欄位 |
| **關網 Gateweb** (`gateweb`) | ✅ | ✅ | ✅ | ✅ | 已通過 | 異步取號需呼叫查詢 API |

## ECPay（合併 B2C/B2B）

| 項目 | 值 |
|---|---|
| Provider class | `EcpayProvider` (delegates to `EcpayB2CProvider` / `EcpayB2BProvider`) |
| Code | `ecpay` |
| 路由規則 | `Einvoice.BuyerTaxId` 為空 → B2C；有值 → B2B |
| 沙盒 base | `https://einvoice-stage.ecpay.com.tw` |
| 沙盒測試憑證 | MerchantID `2000132` / HashKey `ejCk326UnaZWKisg` / HashIV `q9jcZX8Ib9LM8wYk` |
| 加密 | AES-128-CBC + URLEncode + Base64 |

**B2C endpoint**：`/B2CInvoice/{Issue|Invalid|Allowance|AllowanceByCollegiate|AllowanceInvalid}`
**B2B endpoint**：`/B2BInvoice/{Issue|Invalid|Allowance|CancelAllowance}`（注意：B2B 作廢折讓是 `CancelAllowance` 不是 `AllowanceInvalid`）

**B2B 預先設定**：買方統編須先用 `/B2BInvoice/MaintainMerchantCustomerData` 註冊。沙盒已預註冊：
- `22099131` = 台積電股份有限公司

**實測發票號碼樣本**：
- B2C：`JU11005492 / 559 / 60` 等（JU 字軌）
- B2B：`IM41000487 / 488` 等（IM 字軌）
- B2B 折讓：`2605100136338577`

**重要欄位差異**：
- B2C `vat=1`（小寫，注意大小寫敏感）
- B2C 含稅 ItemPrice / ItemAmount；B2B 必須未稅 ItemPrice / ItemAmount + 額外 ItemTax
- B2B 作廢用 `InvoiceNumber`（B2C 用 `InvoiceNo`）
- B2B 折讓 ItemName 必須與原發票對應商品完全一致（規格明文）
- `AllowanceNotify`：S/E/A/N（簡訊/Email/兩者/不通知），不是 paper/online
- `Print=1` 時 CustomerAddr 必填（從 `Partner.Address` 取）

## ezPay（B2C+B2B）

| 項目 | 值 |
|---|---|
| Provider class | `EzpayProvider` |
| Code | `ezpay` |
| 路由規則 | 同一個 endpoint，`Category` 欄位區分 B2C/B2B |
| 沙盒 base | `https://cinv.ezpay.com.tw` |
| 商店端 | 旭日資訊科技 (MerchantID `310588623`) |
| 加密 | AES-256-CBC + .NET 內建 16-byte PKCS7 + 小寫 hex |

**Endpoint**：`POST /Api/{invoice_issue|invoice_invalid|allowance_issue|allowanceInvalid}`

**實測樣本**：
- B2C 發票：`AC00000001 / 02 / 03 / 04`
- B2B 發票：`AC00000005 / 06`
- 折讓單號：`A260510012251844`

**沙盒商店啟用步驟**（user 已完成）：
1. 申請開通電子發票服務
2. 設定字軌類別 07、字軌起訖號碼、起訖日期（生效日 ≤ 今天）

**重要規則**：
- HashKey 必為 32 字元、HashIV 必為 16 字元
- 注意：PDF 範例寫的「32-byte block PKCS7」會導致 KEY10002，實際要用 .NET 標準 16-byte PKCS7
- `MerchantOrderNo` 折讓必須與原開立一致（用 Note `ORDERNO=...` 透傳）
- `InvalidReason` 中文易觸 IAI10004，建議純英數
- 折讓單金額必須與 items 加總一致

## Gateweb

| 項目 | 值 |
|---|---|
| Provider class | `GatewebProvider` |
| Code | `gateweb` |
| 認證流程 | `/api/authenticate` (username/password) → id_token → Bearer |
| 沙盒 base | `https://sstest.gwis.com.tw` |
| 商店端 | sellerIdentifier `70570263` |
| 加密 | 無 AES，僅 JWT auth |

**Endpoint**：
- 開立 `POST /api/v1/simplified/C0403?domestic=true&companyKey={key}`
- 作廢 `POST /api/v1/simplified/C0503?domestic=true&companyKey={key}`
- 折讓 `POST /api/v1/simplified/D0403?domestic=true&companyKey={key}`
- 作廢折讓 `POST /api/v1/simplified/D0503?domestic=true&companyKey={key}`
- 異步取號 `GET /api/v1/invoice/{sellerIdentifier}/{relateNumber}`

**平台欄位對應（DB → Gateweb）**：
- `MerchantId` → companyKey (`70570263_API`)
- `ApiKey` → username
- `ApiSecret` → password
- `ExtraParams` (JSON) → `{"sellerDepartment":"70570263_API"}` (印表機名稱)

**實測樣本**：
- 發票號碼（migNumber）：`JB10002205 / 07 / 08 / 09`
- 折讓單 relateNumber：`ALW099999195509`

**異步特性**：
- C0403 POST 只回 `{typeCode:0, errors:[{errorMessage:"Success"}]}`，不附發票號碼
- 必須再呼叫 GET `/api/v1/invoice/{seller}/{relate}` 取 `migData.migNumber`
- `uploadStatus=P` (Pending) 表示異步處理中；`migNumber` 一指派即可拿，不必等 `Y/C`

**重要規則**：
- B2C（無買方統編）+應稅時，`taxAmount=0`、`salesAmount=含稅總額`（typeCode 1 規則）
- C0503/D0403/D0503 用 `sellerId/buyerId`（不同於 C0403 的 `sellerIdentifier/buyerIdentifier`）
- 作廢/折讓的 `relateNumber` 是「原發票的 relateNumber」（不是作廢自己的），透過 `Einvoice.Note = "ORDERNO=..."` 透傳
- D0403 的 detail 用未稅金額；折讓單 relateNumber 限 16 字元
- 沙盒帳號需 Gateweb 客服綁定 sellerIdentifier；公開 example 統編（23639781）對私人 companyKey 會回 typeCode 15

## 通用診斷端點

`/Einvoice/Diagnose?code=<code>&op=<op>&number=<inv>&allowanceNumber=<aln>&orderNo=<originalRelate>&b2bMode=true|false`

- `op` 值：`issue` / `invalid` / `allowance` / `allowance_invalid` / `b2b_register`（僅 ECPay）/ `selftest`（僅 ezPay）
- `b2bMode=true` 強制 B2B（dummy 加買方統編 22099131）
- `orderNo` 給 ezPay/Gateweb 折讓鏈路時用，作為原發票識別

## 已知 / 待辦

- [ ] EditPlatform 加 ExtraParams 編輯欄位（task #30）— 目前要直接 SQL 改
- [ ] Gateweb 異步處理：實際 production 用，建議在 `Einvoice` entity 加 `ProviderRelateNumber` 欄位永久存原發票 relateNumber，避免靠 `Note` hack
- [ ] B2B 確認流程（ECPay）：規格有 `Confirm` 系列 endpoint，本實作未串
