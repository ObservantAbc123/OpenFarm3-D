namespace EmailService.Services;

public class EmailTemplateRenderer : IEmailTemplateRenderer
{
    public string Render(string templateFile, IDictionary<string, string> replacements)
    {
        var baseDir = AppContext.BaseDirectory;
        var path = Path.Combine(baseDir, "Templates", templateFile);
        if (!File.Exists(path)) path = Path.Combine(Directory.GetCurrentDirectory(), "Templates", templateFile);

        var html = File.ReadAllText(path);

        replacements.TryAdd("[COMPANY_NAME]", "OpenFarm");

        var url = Environment.GetEnvironmentVariable("COMPANY_LOGO_URL");
        replacements["[COMPANY_LOGO_URL]"] = url ?? string.Empty;

        return replacements.Aggregate(html, (current, kv) => current.Replace(kv.Key, kv.Value));
    }
}