namespace EmailService.Services;

public interface IEmailTemplateRenderer
{
    string Render(string templateFile, IDictionary<string, string> replacements);
}