using System.Security.Cryptography;
using System.Text;

namespace GpnuNetwork.Core.Utils;

public static class EncryptHelper
{
    public static string RsaEncrypt(string content, string exponentHex, string modulusHex)
    {
        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters
        {
            Exponent = Convert.FromHexString(exponentHex),
            Modulus = Convert.FromHexString(modulusHex)
        });

        var data = Encoding.UTF8.GetBytes(content);
        var encryptedData = rsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
        return Convert.ToBase64String(encryptedData);
    }
}