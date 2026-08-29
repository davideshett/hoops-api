using FluentValidation;
using Hoops.Modules.Identity.Contracts;

namespace Hoops.Api.Validation;

/// <summary>Shape validation for <see cref="RegisterRequest"/>. Domain rules live in the domain.</summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    /// <summary>Configures the rules.</summary>
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(256);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
    }
}

/// <summary>Shape validation for <see cref="LoginRequest"/>.</summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>Configures the rules.</summary>
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}

/// <summary>Shape validation for <see cref="RefreshRequest"/>.</summary>
public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    /// <summary>Configures the rules.</summary>
    public RefreshRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

/// <summary>Shape validation for <see cref="LogoutRequest"/>.</summary>
public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    /// <summary>Configures the rules.</summary>
    public LogoutRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty();
}

/// <summary>Shape validation for <see cref="CreateOrganisationRequest"/>.</summary>
public sealed class CreateOrganisationRequestValidator : AbstractValidator<CreateOrganisationRequest>
{
    /// <summary>Configures the rules.</summary>
    public CreateOrganisationRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(80)
            .Matches("^[a-zA-Z0-9-]+$").WithMessage("Slug may contain only letters, digits, and hyphens.");
        RuleFor(x => x.CountryCode).Length(2).When(x => !string.IsNullOrEmpty(x.CountryCode));
    }
}

/// <summary>Shape validation for <see cref="InviteMemberRequest"/>.</summary>
public sealed class InviteMemberRequestValidator : AbstractValidator<InviteMemberRequest>
{
    /// <summary>Configures the rules.</summary>
    public InviteMemberRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Role).NotEmpty();
    }
}

/// <summary>Shape validation for <see cref="ChangeRoleRequest"/>.</summary>
public sealed class ChangeRoleRequestValidator : AbstractValidator<ChangeRoleRequest>
{
    /// <summary>Configures the rules.</summary>
    public ChangeRoleRequestValidator() => RuleFor(x => x.Role).NotEmpty();
}
