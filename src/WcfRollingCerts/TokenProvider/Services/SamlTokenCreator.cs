using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace TokenProvider.Services;

public class SamlTokenCreator
{
    public string CreateSamlToken(string username, X509Certificate2 signingCertificate, string tokenId)
    {
        var now = DateTime.UtcNow;
        var expiry = now.AddHours(1);

        // Create SAML assertion XML with proper ID structure
        var doc = new XmlDocument();
        doc.PreserveWhitespace = false;
        
        var xmlStr = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<saml:Assertion 
    xmlns:saml=""urn:oasis:names:tc:SAML:2.0:assertion""
    xmlns:ds=""http://www.w3.org/2000/09/xmldsig#""
    ID=""_{tokenId}""
    Version=""2.0""
    IssueInstant=""{now:yyyy-MM-ddTHH:mm:ssZ}"">
    
    <saml:Issuer>TokenProvider_{Environment.MachineName}</saml:Issuer>
    
    <saml:Subject>
        <saml:NameID Format=""urn:oasis:names:tc:SAML:2.0:nameid-format:unspecified"">{username}</saml:NameID>
    </saml:Subject>
    
    <saml:Conditions 
        NotBefore=""{now:yyyy-MM-ddTHH:mm:ssZ}""
        NotOnOrAfter=""{expiry:yyyy-MM-ddTHH:mm:ssZ}"">
        <saml:AudienceRestriction>
            <saml:Audience>WcfRollingCerts</saml:Audience>
        </saml:AudienceRestriction>
    </saml:Conditions>
    
    <saml:AuthnStatement 
        AuthnInstant=""{now:yyyy-MM-ddTHH:mm:ssZ}"">
        <saml:AuthnContext>
            <saml:AuthnContextClassRef>urn:oasis:names:tc:SAML:2.0:ac:classes:X509</saml:AuthnContextClassRef>
        </saml:AuthnContext>
    </saml:AuthnStatement>
    
    <saml:AttributeStatement>
        <saml:Attribute Name=""Username"">
            <saml:AttributeValue>{username}</saml:AttributeValue>
        </saml:Attribute>
        <saml:Attribute Name=""CertificateThumbprint"">
            <saml:AttributeValue>{signingCertificate.Thumbprint}</saml:AttributeValue>
        </saml:Attribute>
        <saml:Attribute Name=""TokenId"">
            <saml:AttributeValue>{tokenId}</saml:AttributeValue>
        </saml:Attribute>
    </saml:AttributeStatement>
</saml:Assertion>";

        doc.LoadXml(xmlStr);

        // Sign the assertion
        var signedXml = new SignedXml(doc);
        signedXml.SigningKey = signingCertificate.GetRSAPrivateKey();
        
        // Set the canonicalization method for SignedInfo
        signedXml.SignedInfo.CanonicalizationMethod = SignedXml.XmlDsigExcC14NTransformUrl;
        
        // Create reference to the assertion with the proper ID
        var reference = new Reference($"#_{tokenId}");
        reference.AddTransform(new XmlDsigEnvelopedSignatureTransform());
        reference.AddTransform(new XmlDsigExcC14NTransform());
        signedXml.AddReference(reference);
        
        // Add key info
        var keyInfo = new KeyInfo();
        keyInfo.AddClause(new KeyInfoX509Data(signingCertificate));
        signedXml.KeyInfo = keyInfo;
        
        // Compute signature
        signedXml.ComputeSignature();
        
        // Insert signature into the assertion after the Issuer element
        var signatureElement = signedXml.GetXml();
        var assertionElement = doc.DocumentElement!;
        
        var issuerElement = assertionElement.SelectSingleNode("saml:Issuer", GetNamespaceManager(doc));
        if (issuerElement?.NextSibling != null)
        {
            assertionElement.InsertAfter(signatureElement, issuerElement);
        }
        else
        {
            assertionElement.AppendChild(signatureElement);
        }
        
        return doc.OuterXml;
    }
    
    private XmlNamespaceManager GetNamespaceManager(XmlDocument doc)
    {
        var nsmgr = new XmlNamespaceManager(doc.NameTable);
        nsmgr.AddNamespace("saml", "urn:oasis:names:tc:SAML:2.0:assertion");
        nsmgr.AddNamespace("ds", "http://www.w3.org/2000/09/xmldsig#");
        return nsmgr;
    }
}