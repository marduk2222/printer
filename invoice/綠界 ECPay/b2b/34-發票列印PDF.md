---
source: https://developers.ecpay.com.tw/53383/
section: B2B 電子發票
title: 發票列印 PDF
fetched: 2026-05-09
status: ok
---

# B2B 電子發票 API - 發票列印 PDF

## 應用場景

- 特店可使用此 API 取得單一發票 PDF 檔
- 同一 IP，10 秒內最多只可呼叫 2 次

## API 介接網址

| 環境 | URL |
|------|-----|
| 測試環境 | https://einvoice-stage.ecpay.com.tw/B2BInvoice/DownloadB2BPdf |
| 正式環境 | https://einvoice.ecpay.com.tw/B2BInvoice/DownloadB2BPdf |

## HTTPS 傳輸協定

- Content Type：application/json
- HTTP Method：POST

## 特店傳入參數（JSON 格式）

| 參數名稱 | 型別 | 長度 | 必填 | 說明 | 預設值 |
|---------|------|------|------|------|--------|
| PlatformID | String | 10 | N | 特約合作平台商代號（一般廠商放空值） | - |
| MerchantID | String | 10 | Y | 特店編號 | - |
| RqHeader | Object | - | Y | 傳入資料物件 | - |
| RqHeader.Timestamp | Number | - | Y | 傳入時間戳（GMT+8 Unix TimeStamp） | - |
| Data | String | - | Y | 加密資料（加密過的 JSON 格式） | - |

### 特店傳入參數範例

```json
{
  "MerchantID": "2000132",
  "RqHeader": {
    "Timestamp": 1525168923
  },
  "Data": "加密資料"
}
```

## Data 參數說明（JSON 格式）

| 參數名稱 | 型別 | 長度 | 必填 | 說明 | 預設值 |
|---------|------|------|------|------|--------|
| MerchantID | String | 10 | Y | 特店編號 | - |
| InvoiceCategory | Int | - | N | B2B 發票種類（0：銷項；1：進項） | 0 |
| InvoiceNo | String | 10 | Y | 發票號碼（2 碼字軌+8 碼數字） | - |
| InvoiceDate | String | 20 | Y | 發票開立日期（格式：yyyy-MM-dd 或 yyyy/MM/dd） | - |
| PrintStyle | Int | - | N | 發票列印格式（1：A4；2：A5） | 1 |

### Data 參數範例

```json
{
  "MerchantID": "2000132",
  "InvoiceCategory": 0,
  "InvoiceNo": "UV11100016",
  "InvoiceDate": "2018-10-28",
  "PrintStyle": 1
}
```

## 綠界回傳參數格式

### 成功回傳

- Content Type：application/pdf
- Content-Disposition：attachment; filename="xxxx.pdf"
- 回傳 PDF 文件的二進位檔，可直接存成 .pdf 檔

### 失敗回傳（JSON 格式）

| 參數名稱 | 型別 | 長度 | 說明 |
|---------|------|------|------|
| PlatformID | String | 10 | 特約合作平台商代號 |
| MerchantID | String | 10 | 特店編號 |
| RpHeader | Object | - | 回傳資料物件 |
| RpHeader.Timestamp | Number | - | 回傳時間（Unix timestamp） |
| TransCode | Int | - | 回傳代碼（1 代表傳輸成功） |
| TransMsg | String | 200 | 回傳訊息 |
| Data | String | - | 加密資料（加密過的 JSON 格式） |

### 綠界 Response 參數範例

```json
{
  "MerchantID": "2000132",
  "RpHeader": {
    "Timestamp": "1525169058"
  },
  "TransCode": 1,
  "TransMsg": "",
  "Data": "…"
}
```

## Data 參數說明（失敗回傳）

| 參數名稱 | 型別 | 長度 | 說明 |
|---------|------|------|------|
| RtnCode | Int | - | 回應代碼 |
| RtnMsg | String | 200 | 回應訊息 |

### Data 參數範例

```json
{
  "RtnCode": 0,
  "RtnMsg": "無法下載進項發票"
}
```

## 時間驗證注意事項

- 驗證時間區間暫訂為 10 分鐘內有效
- 若超過驗證時間則無法建立此次訂單
- 合作特店須進行主機時間校正，避免時差問題

## 加密方法

詳見官方文件「[參數加密方式說明](https://developers.ecpay.com.tw/?p=15008)」
