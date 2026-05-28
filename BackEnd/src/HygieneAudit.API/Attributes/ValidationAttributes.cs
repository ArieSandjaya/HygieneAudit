using System;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace HygieneAudit.API.Attributes;

public class ValidDateAttribute : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is DateTime date)
        {
            if (date > DateTime.UtcNow.AddDays(1))
                return new ValidationResult("Date cannot be in the future.");
            if (date < DateTime.UtcNow.AddYears(-1))
                return new ValidationResult("Date cannot be more than 1 year old.");
        }
        return ValidationResult.Success;
    }
}

public class NoHtmlAttribute : ValidationAttribute
{
    private static readonly Regex HtmlRegex = new Regex(@"<.*?>", RegexOptions.Compiled);

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string str && HtmlRegex.IsMatch(str))
            return new ValidationResult("HTML tags are not allowed.");
        return ValidationResult.Success;
    }
}

public class SafeStringAttribute : ValidationAttribute
{
    private static readonly Regex SqlInjectionRegex = new Regex(
        @"(--|;|--|\/\*|\*\/|xp_|sp_|exec\s|execute\s|insert\s|delete\s|update\s|drop\s|union\s|select\s)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is string str && SqlInjectionRegex.IsMatch(str))
            return new ValidationResult("Potentially dangerous content detected.");
        return ValidationResult.Success;
    }
}
