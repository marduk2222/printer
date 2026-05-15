---
source: invoice/藍新 ezPay/EZP_INVI_1_2_2.pdf
section: ezPay 藍新 電子發票
fetched: 2026-05-09
---

# ezPay 藍新 電子發票 API 規格

文件版本：EZP_INVI_1.2.2（2024/4/22）
平台：簡單行動支付股份有限公司 ezPay 電子發票加值服務平台

---

## 1. 認證憑證

向 ezPay 申請會員、開立商店後可取得下列三項串接資訊，所有 API 都使用同一組憑證：

| 欄位 | 說明 | 取得方式 |
|---|---|---|
| MerchantID | 商店代號（Varchar 15）；以未加密方式以 `MerchantID_` 參數送出 | ezPay 後台商店資料 |
| HashKey | API 串接金鑰（32 字元），AES-256-CBC 的 Key | 商店建立後於後台取得 |
| HashIV | API 串接金鑰（16 字元），AES-256-CBC 的 IV | 商店建立後於後台取得 |

PHP 範例中的 Key/IV 長度：
- HashKey 長度 32（例：`abcdefghijklmnopqrstuvwxyzabcdef`）
- HashIV 長度 16（例：`1234567891234567`）

> 注意：所有 POST 表單參數中只有 `MerchantID_` 與 `PostData_` 兩個欄位，欄名後方都帶底線 `_`。

---

## 2. 共用規範（加密 / Header）

### 2.1 傳輸方式

- HTTP method：`POST`
- Content-Type：`application/x-www-form-urlencoded`（標準 Form Post）
- 編碼：UTF-8
- 回傳：Web Service 文字回應；格式由 `RespondType` 決定（`JSON` 或 `String`）

### 2.2 共通 Form 欄位

| 參數 | 必填 | 型態 | 說明 |
|---|---|---|---|
| MerchantID_ | V | Varchar(15) | 商店代號（不加密） |
| PostData_ | V | text | 其他所有欄位 URL-encode 後再 AES 加密，最後轉小寫 hex |

### 2.3 PostData_ 共通內含欄位

下列欄位每個 API 的 PostData_ 都會出現：

| 參數 | 必填 | 型態 | 說明 |
|---|---|---|---|
| RespondType | V | Varchar(5) | `JSON` 或 `String` |
| Version | V | Varchar(5) | 各 API 固定版本，見各章節 |
| TimeStamp | V | Varchar(30) | Unix Timestamp 秒數，例：`1400137200` |

### 2.4 加密規則（PostData_）

| 項目 | 值 |
|---|---|
| 演算法 | AES-256-CBC（PHP 7+：`AES-256-CBC` / Rijndael 128 區塊） |
| Key | HashKey（32 byte，UTF-8） |
| IV | HashIV（16 byte，UTF-8） |
| Padding | PKCS#7（每 32 byte 區塊補；C# 範例自實作 `AddPKCS7Padding`，AES `Padding=None` + 手動補；PHP 7+ `OPENSSL_RAW_DATA \| OPENSSL_ZERO_PADDING`） |
| 編碼順序 | (1) 欄位陣列轉 query string（`http_build_query`，已 URL-encode）→ (2) 補 PKCS#7 → (3) AES-256-CBC 加密 → (4) 二進位 → 小寫 hex 字串 |

C# 加密重點（節錄自附件一）：

```csharp
var aes = new RijndaelManaged();
aes.Key = Encoding.UTF8.GetBytes(HashKey);   // 32 bytes
aes.IV  = Encoding.UTF8.GetBytes(HashIV);    // 16 bytes
aes.Mode = CipherMode.CBC;
aes.Padding = PaddingMode.None;              // 自行 PKCS#7
var bytes = AddPKCS7Padding(Encoding.UTF8.GetBytes(queryString), 32);
var cipher = aes.CreateEncryptor().TransformFinalBlock(bytes, 0, bytes.Length);
return ByteArrayToHex(cipher).ToLower();
```

### 2.5 CheckCode 驗證規則（附件二）

回傳資料中 `CheckCode` 用於驗證來源是否為 ezPay：

1. 取下列 5 欄位：`InvoiceTransNo`、`MerchantID`、`MerchantOrderNo`、`RandomNum`、`TotalAmt`，按字母 A→Z 排序。
2. 用 `&` 串接成 query string。
3. 前面加 `HashIV={HashIV}&`，後面加 `&HashKey={HashKey}`。
4. SHA256 雜湊後轉大寫。

範例：

```
HashIV=1234567891234567&InvoiceTransNo=14061313541640927&MerchantID=3622183
&MerchantOrderNo=201409170000001&RandomNum=0142&TotalAmt=500
&HashKey=abcdefghijklmnopqrstuvwxyzabcdef
↓ SHA256 + ToUpper
303AB800650B724733B5D91CBCE075D9EA09E4CDE9CD33461D45F07D5EC7EECB
```

### 2.6 共通回傳欄位

| 欄位 | 型態 | 說明 |
|---|---|---|
| Status | Varchar(10) | `SUCCESS` 或錯誤代碼 |
| Message | Varchar(30) | 文字訊息（URL-encoded） |
| Result | text | JSON 字串（RespondType=JSON 時） |
| EndStr | Varchar(2) | 固定 `##`（RespondType=String 時，作為結尾旗標） |

---

## 3. 開立發票 Issue

### 3.1 Endpoint

| 環境 | URL |
|---|---|
| 測試 | `https://cinv.ezpay.com.tw/Api/invoice_issue` |
| 正式 | `https://inv.ezpay.com.tw/Api/invoice_issue` |

- Method：POST  Content-Type：`application/x-www-form-urlencoded`
- Version：固定 `1.5`

### 3.2 PostData_ 必填參數

| 欄位 | 必填 | 型態 | 範例 / 說明 |
|---|---|---|---|
| RespondType | V | Varchar(5) | `JSON` / `String` |
| Version | V | Varchar(5) | `1.5` |
| TimeStamp | V | Varchar(30) | Unix 秒數 |
| TransNum | | Varchar(20) | ezPay 簡單付交易序號（金流串接時帶） |
| MerchantOrderNo | V | Varchar(20) | 商店自訂訂單編號（限英、數字、`_`，同店唯一） |
| Status | V | Varchar(1) | `1`=即時開立、`0`=等待觸發、`3`=預約自動 |
| CreateStatusTime | | Date | `Status=3` 時必填，格式 `YYYY-MM-DD` |
| Category | V | Varchar(5) | `B2B` / `B2C` |
| BuyerName | V | Varchar(60/30) | B2B：營業人名稱（60 字元，不足以統編帶入）；B2C：個人/識別碼（30 字元） |
| BuyerUBN | (B2B 必填) | Varchar(8) | 純數字統編 |
| BuyerAddress | | Varchar(100) | 買受人地址 |
| BuyerEmail | | Varchar(50) | CarrierType=2 時必填 |
| CarrierType | | Varchar(2) | `0`=手機條碼、`1`=自然人憑證、`2`=ezPay 電子發票載具；無載具則空 |
| CarrierNum | | Varchar(50) | 載具號碼；ezPay 載具帶買受人代號（需 `rawurlencode`） |
| LoveCode | | Int(7) | 3~7 碼捐贈碼；與 CarrierType 互斥 |
| PrintFlag | V | Varchar(1) | `Y`=索取紙本、`N`=不索取；B2B 必填 Y |
| KioskPrintFlag | | Varchar(1) | `1`=中獎後開放至超商 Kiosk 列印；CarrierType=2 適用 |
| TaxType | V | Varchar(2) | `1`=應稅、`2`=零稅率、`3`=免稅、`9`=混合（限 B2C） |
| TaxRate | V | Float(6,4) | 應稅一般 5、特種帶實際稅率（不含 %）；零稅率/免稅帶 0 |
| CustomsClearance | | Varchar(1) | 零稅率時：`1`=非經海關、`2`=經海關 |
| Amt | V | Int(10) | 銷售額合計（未稅）；TaxType=9 時為 `AmtSales+AmtZero+AmtFree` |
| AmtSales | (TaxType=9) | Int(10) | 應稅未稅銷售額 |
| AmtZero | (TaxType=9) | Int(10) | 零稅率銷售額 |
| AmtFree | (TaxType=9) | Int(10) | 免稅銷售額 |
| TaxAmt | V | Int(10) | 稅額 |
| TotalAmt | V | Int(10) | 發票金額（含稅）= 銷售額 + 稅額 |
| ItemName | V | Varchar(30) | 多項以 `\|` 分隔，例：`商品一\|商品二` |
| ItemCount | V | Int(5) | 多項以 `\|` 分隔，例：`1\|2` |
| ItemUnit | V | Varchar(2) | 中文 2 字或英數 6 字，多項以 `\|` 分隔 |
| ItemPrice | V | Int(10) | B2B 未稅 / B2C 含稅；多項以 `\|` 分隔 |
| ItemAmt | V | Int(10) | 數量×單價；多項以 `\|` 分隔 |
| ItemTaxType | (TaxType=9) | Int(2) | `1`/`2`/`3`，多項以 `\|` 分隔 |
| Comment | | Varchar(200) | 發票備註 |

### 3.3 回傳欄位（Result 內）

| 欄位 | 型態 | 說明 |
|---|---|---|
| MerchantID | Varchar(15) | 商店代號 |
| InvoiceTransNo | Varchar(20) | ezPay 電子發票開立序號 |
| MerchantOrderNo | Varchar(20) | 商店自訂編號 |
| TotalAmt | Int(10) | 發票金額 |
| InvoiceNumber | Varchar(10) | 發票號碼（僅 Status=1 即時時回傳） |
| RandomNum | Varchar(4) | 4 碼防偽隨機碼 |
| CreateTime | DateTime | 開立時間 |
| CheckCode | Varchar(64) | SHA256 驗證碼 |
| BarCode | Varchar(19) | 條碼（PrintFlag=Y 時提供） |
| QRcodeL | Varchar(140) | 左 QRCode（PrintFlag=Y 時提供） |
| QRcodeR | Varchar(140) | 右 QRCode（PrintFlag=Y 時提供） |

### 3.4 Request / Response 範例

Request body（成功送出後）：

```
MerchantID_=3622183&PostData_=70a61189d7dc0f6abefe7643da144af5...（小寫 hex）
```

Response（JSON）：

```json
{
  "Status":"SUCCESS",
  "Message":"電子發票開立成功",
  "Result":"{\"CheckCode\":\"00E108DF7DE8...\",\"MerchantID\":\"3502275\",\"MerchantOrderNo\":\"201511031758110280\",\"InvoiceNumber\":\"DS12223139\",\"TotalAmt\":348,\"InvoiceTransNo\":\"15110317583641325\",\"RandomNum\":\"4253\",\"CreateTime\":\"2015-11-03 17:58:36\",\"BarCode\":\"10412DS122231394253\",\"QRcodeL\":\"DS12223139...\",\"QRcodeR\":\"**...\"}"
}
```

### 3.5 觸發開立發票（Status=0/3 用）

| 環境 | URL |
|---|---|
| 測試 | `https://cinv.ezpay.com.tw/Api/invoice_touch_issue` |
| 正式 | `https://inv.ezpay.com.tw/Api/invoice_touch_issue` |

Version=`1.0`。PostData_ 額外帶 `InvoiceTransNo`、`MerchantOrderNo`、`TotalAmt`。

---

## 4. 作廢發票 Invalid

### 4.1 Endpoint

| 環境 | URL |
|---|---|
| 測試 | `https://cinv.ezpay.com.tw/Api/invoice_invalid` |
| 正式 | `https://inv.ezpay.com.tw/Api/invoice_invalid` |

- Method：POST  Content-Type：`application/x-www-form-urlencoded`
- Version：固定 `1.0`
- 限制：奇數月 14 日前，可作廢前兩個月開立之發票（例：7/14 前可作廢 5/1–6/30 發票）

### 4.2 PostData_ 必填參數

| 欄位 | 必填 | 型態 | 說明 |
|---|---|---|---|
| RespondType | V | Varchar(5) | `JSON` / `String` |
| Version | V | Varchar(5) | `1.0` |
| TimeStamp | V | Varchar(30) | Unix 秒數 |
| InvoiceNumber | V | Varchar(10) | 欲作廢之發票號碼 |
| InvalidReason | V | Varchar(6) | 中文 6 字或英文 20 字 |

### 4.3 回傳欄位（Result 內）

| 欄位 | 型態 | 說明 |
|---|---|---|
| MerchantID | Varchar(15) | 商店代號 |
| InvoiceNumber | Varchar(10) | 作廢之發票號碼 |
| CreateTime | DateTime | 作廢時間 |
| CheckCode | Varchar(64) | SHA256 驗證碼 |

### 4.4 Response 範例

```json
{
  "Status":"SUCCESS",
  "Message":"電子發票作廢開立成功",
  "Result":"{\"CheckCode\":\"01DD7B45A33B...\",\"MerchantID\":\"3459997\",\"InvoiceNumber\":\"OU00122220\",\"CreateTime\":\"2015-07-16 17:00:33\"}"
}
```

---

## 5. 開立折讓 Allowance

### 5.1 Endpoint

| 環境 | URL |
|---|---|
| 測試 | `https://cinv.ezpay.com.tw/Api/allowance_issue` |
| 正式 | `https://inv.ezpay.com.tw/Api/allowance_issue` |

- Method：POST  Content-Type：`application/x-www-form-urlencoded`
- Version：固定 `1.3`

### 5.2 PostData_ 必填參數

| 欄位 | 必填 | 型態 | 說明 |
|---|---|---|---|
| RespondType | V | Varchar(5) | `JSON` / `String` |
| Version | V | Varchar(5) | `1.3` |
| TimeStamp | V | Varchar(30) | Unix 秒數 |
| InvoiceNo | V | Varchar(10) | 開立折讓的發票號碼 |
| MerchantOrderNo | V | Varchar(20) | 原開立發票時的自訂編號 |
| ItemName | V | Varchar(30) | 折讓商品名稱，多項以 `\|` 分隔 |
| ItemCount | V | Int(5) | 折讓數量，多項以 `\|` 分隔 |
| ItemUnit | V | Varchar(2) | 折讓單位，多項以 `\|` 分隔 |
| ItemPrice | V | Int(10) | 折讓單價（可未稅可含稅，含稅時 ItemTaxAmt=0）|
| ItemAmt | V | Int(10) | 折讓小計 = 數量×單價 |
| TaxTypeForMixed | (混合稅率時) | Int(2) | `1`=應稅、`2`=零稅率、`3`=免稅 |
| ItemTaxAmt | V | Int(10) | 折讓商品稅額；含稅時=0；多項以 `\|` 分隔 |
| TotalAmt | V | Int(10) | 折讓總金額 |
| BuyerEmail | | Varchar(50) | 買受人 Email（折讓通知） |
| Status | V | Varchar(1) | `0`=不立即確認折讓、`1`=立即確認折讓 |

### 5.3 回傳欄位（Result 內）

| 欄位 | 型態 | 說明 |
|---|---|---|
| MerchantID | Varchar(15) | 商店代號 |
| AllowanceNo | Varchar(20) | 折讓號 |
| InvoiceNumber | Varchar(10) | 發票號碼 |
| MerchantOrderNo | Varchar(20) | 自訂編號 |
| AllowanceAmt | Int(10) | 折讓金額 |
| RemainAmt | Int(10) | 折讓後剩餘發票金額 |
| CheckCode | Varchar(64) | SHA256 驗證碼 |

### 5.4 Response 範例

```json
{
  "Status":"SUCCESS",
  "Message":"電子發票開立成功",
  "Result":"{\"MerchantID\":\"3622183\",\"AllowanceNo\":\"A151015111705007\",\"MerchantOrderNo\":\"202E19\",\"AllowanceAmt\":\"500\",\"RemainAmt\":\"0\",\"CheckCode\":\"F3BB07F44794...\"}"
}
```

### 5.5 觸發確認/取消折讓（Status=0 時補執行）

| 環境 | URL |
|---|---|
| 測試 | `https://cinv.ezpay.com.tw/Api/allowance_touch_issue` |
| 正式 | `https://inv.ezpay.com.tw/Api/allowance_touch_issue` |

Version=`1.0`。PostData_ 帶：`AllowanceStatus`（`C`=確認、`D`=取消）、`AllowanceNo`、`MerchantOrderNo`、`TotalAmt`。

---

## 6. 作廢折讓 AllowanceInvalid

### 6.1 Endpoint

| 環境 | URL |
|---|---|
| 測試 | `https://cinv.ezpay.com.tw/Api/allowanceInvalid` |
| 正式 | `https://inv.ezpay.com.tw/Api/allowanceInvalid` |

- Method：POST  Content-Type：`application/x-www-form-urlencoded`
- Version：固定 `1.0`
- 限制：須為已確認之折讓；作廢後隔日上傳財政部

### 6.2 PostData_ 必填參數

| 欄位 | 必填 | 型態 | 說明 |
|---|---|---|---|
| RespondType | V | Varchar(5) | `JSON` / `String` |
| Version | V | Varchar(5) | `1.0` |
| TimeStamp | V | Varchar(30) | Unix 秒數 |
| AllowanceNo | V | Varchar(25) | 欲作廢之折讓號 |
| InvalidReason | V | Varchar(6) | 中文 6 字或英文 20 字 |

### 6.3 回傳欄位（Result 內）

| 欄位 | 型態 | 說明 |
|---|---|---|
| MerchantID | Varchar(15) | 商店代號 |
| AllowanceNo | Varchar(25) | 作廢之折讓號 |
| CreateTime | DateTime | 作廢時間 |
| CheckCode | Varchar(64) | SHA256 驗證碼 |

### 6.4 Response 範例

```json
{
  "Status":"SUCCESS",
  "Message":"作廢折讓成功",
  "Result":"{\"MerchantID\":\"3622183\",\"AllowanceNo\":\"A180528095517632\",\"CreateTime\":\"2018-05-28 09:55:45\",\"CheckCode\":\"1C428B8EF5E89C3CB303567AFF04F71BA3803103D162948F3AEAC55831E7C0AA\"}"
}
```

---

## 7. 載具與其他列舉值

### 7.1 載具類別 CarrierType

| 代號 | 說明 | 編號格式 |
|---|---|---|
| (空) | 無載具（紙本/捐贈） | — |
| `0` | 手機條碼載具 | 第 1 碼 `/` + 7 碼大寫英數，可用字元：0–9、A–Z、`+`、`-`、`.`（共 39 個） |
| `1` | 自然人憑證條碼載具 | 2 碼大寫英字 + 14 碼數字 |
| `2` | ezPay 電子發票載具 | 賣方統編 + 自訂買受人代號（e-mail/手機/會員編號…）；參數值需 `rawurlencode` |

### 7.2 課稅別 TaxType

| 代號 | 說明 | TaxRate |
|---|---|---|
| `1` | 應稅 | 一般 5；特種帶規定稅率（如 `18`） |
| `2` | 零稅率 | 0；需帶 `CustomsClearance` |
| `3` | 免稅 | 0 |
| `9` | 混合應稅與免稅或零稅率（限 B2C） | 各項目以 `ItemTaxType` 個別標記 |

### 7.3 報關標記 CustomsClearance（零稅率時）

| 代號 | 說明 |
|---|---|
| `1` | 非經海關出口 |
| `2` | 經海關出口 |

### 7.4 商品課稅別 ItemTaxType（TaxType=9 時）

| 代號 | 說明 |
|---|---|
| `1` | 應稅 |
| `2` | 零稅率 |
| `3` | 免稅 |

### 7.5 捐贈碼 LoveCode

- 限 3~7 碼純數字
- 僅 Category=B2C 適用
- 與 CarrierType 互斥
- 受贈單位捐贈碼可至財政部電子發票整合服務平台查詢

### 7.6 開立發票方式 Status

| 代號 | 說明 |
|---|---|
| `1` | 即時開立發票 |
| `0` | 等待觸發開立發票 |
| `3` | 預約自動開立發票（需帶 `CreateStatusTime`） |

### 7.7 發票種類 Category

| 代號 | 說明 |
|---|---|
| `B2B` | 買受人為營業人 |
| `B2C` | 買受人為個人 |

### 7.8 發票字軌類型 InvoiceType（查詢回傳）

| 代號 | 說明 |
|---|---|
| `07` | 一般稅額計算 |
| `08` | 特種稅額計算 |

### 7.9 發票狀態 / 上傳狀態（查詢回傳）

| InvoiceStatus | 說明 |
|---|---|
| `1` | 已開立 |
| `2` | 已作廢 |

| UploadStatus | 說明 |
|---|---|
| `0` | 未上傳 |
| `1` | 已上傳成功 |
| `2` | 上傳中 |
| `3` | 上傳失敗 |
| `4` | 上傳逾時 |

---

## 8. 錯誤代碼

| 錯誤代碼 | 錯誤原因 | 備註 |
|---|---|---|
| KEY10002 | 資料解密錯誤 | |
| KEY10004 | 資料不齊全 | |
| KEY10006 | 商店未申請啟用電子發票 | |
| KEY10007 | 頁面停留超過 30 分鐘 | |
| KEY10010 | 商店代號空白 | |
| KEY10011 | PostData_ 欄位空白 | |
| KEY10012 | 資料傳遞錯誤 | |
| KEY10013 | 資料空白 | |
| KEY10014 | TimeOut | |
| KEY10015 | 發票金額格式錯誤 | |
| INV10003 | 商品資訊格式錯誤或缺少資料 | |
| INV10004 | 商品資訊的商品小計計算錯誤 | |
| INV10006 | 稅率格式錯誤 | |
| INV10012 | 發票金額、課稅別驗證錯誤 | |
| INV10013 | 發票欄位資料不齊全或格式錯誤 | |
| INV10014 | 自訂編號格式錯誤 | |
| INV10015 | 無未稅金額 | |
| INV10016 | 無稅金 | |
| INV10017 | 輸入的版本不支援混合稅率功能 | |
| INV10019 | 資料含有控制碼 | |
| INV10020 | 暫停使用 | |
| INV10021 | 異常終止 | |
| INV20006 | 查無發票資料 | |
| INV70001 | 欄位資料格式錯誤 | |
| INV70002 | 上傳失敗之發票不得作廢 | |
| INV90005 | 未簽定合約或合約已到期 | |
| INV90006 | 可開立張數已用罄 | |
| NOR10001 | 網路連線異常 | |
| LIB10003 | 商店自訂編號重覆 | |
| LIB10005 | 發票已作廢過 | |
| LIB10007 | 無法作廢 | 該張發票已執行過折讓，則無法再作廢 |
| LIB10008 | 超過可作廢期限 | |
| LIB10009 | 發票已開立但未上傳至財政部，無法作廢 | 須上傳財政部完成後才可作廢 |
| IAI10001 | 缺少參數 | 作廢折讓錯誤代碼 |
| IAI10002 | 查詢失敗 | 作廢折讓錯誤代碼 |
| IAI10003 | 更新失敗 | 作廢折讓錯誤代碼 |
| IAI10004 | 參數錯誤 | 作廢折讓錯誤代碼 |
| IAI10005 | 新增失敗 | 作廢折讓錯誤代碼 |
| IAI10006 | 異常終止 | 作廢折讓錯誤代碼 |
