using System.Security.Cryptography;
using System.Text;
using System.Web;

namespace printer.Services.Invoicing.Providers;

/// <summary>
/// 綠界 ECPay B2C / B2B 共用 AES 加密。
/// 流程：JSON → URLEncode → AES-128-CBC + PKCS7 → Base64
/// 解密反向。
/// 參考：invoice/ecpay/b2c/33-參數加密方式說明.md
/// </summary>
public static class EcpayCrypto
{
    public static string Encrypt(string plain, string hashKey, string hashIv)
    {
        // ECPay URLEncode 規範：空白要 +、特定字符大寫處理
        var encoded = HttpUtility.UrlEncode(plain, Encoding.UTF8);

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.BlockSize = 128;
        aes.KeySize = 128;
        aes.Key = Encoding.UTF8.GetBytes(hashKey);
        aes.IV = Encoding.UTF8.GetBytes(hashIv);

        using var encryptor = aes.CreateEncryptor();
        var bytes = Encoding.UTF8.GetBytes(encoded);
        var cipher = encryptor.TransformFinalBlock(bytes, 0, bytes.Length);
        return Convert.ToBase64String(cipher);
    }

    public static string Decrypt(string base64Cipher, string hashKey, string hashIv)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.BlockSize = 128;
        aes.KeySize = 128;
        aes.Key = Encoding.UTF8.GetBytes(hashKey);
        aes.IV = Encoding.UTF8.GetBytes(hashIv);

        using var decryptor = aes.CreateDecryptor();
        var cipher = Convert.FromBase64String(base64Cipher);
        var plain = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
        var encoded = Encoding.UTF8.GetString(plain);
        return HttpUtility.UrlDecode(encoded, Encoding.UTF8);
    }
}
