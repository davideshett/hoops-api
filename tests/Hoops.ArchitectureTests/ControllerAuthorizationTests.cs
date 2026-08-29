using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hoops.ArchitectureTests;

/// <summary>
/// The highest-consequence bug in a national player registry is a silently unprotected endpoint. This
/// test enumerates every controller action and fails the build if any lacks an explicit, method-level
/// <see cref="AuthorizeAttribute"/> or <see cref="AllowAnonymousAttribute"/> — never inherited from
/// the class, never absent. Do not disable it.
/// </summary>
public sealed class ControllerAuthorizationTests
{
    public static IEnumerable<object[]> ActionMethods()
    {
        var apiAssembly = typeof(Program).Assembly;

        var controllers = apiAssembly.GetTypes()
            .Where(t => typeof(ControllerBase).IsAssignableFrom(t) && t is { IsAbstract: false, IsPublic: true });

        foreach (var controller in controllers)
        {
            foreach (var method in ActionsOf(controller))
            {
                yield return [controller.Name, method.Name, method];
            }
        }
    }

    [Theory]
    [MemberData(nameof(ActionMethods))]
    public void Every_action_declares_authorization_explicitly(string controller, string action, MethodInfo method)
    {
        var hasAuthorize = method.GetCustomAttribute<AuthorizeAttribute>(inherit: false) is not null;
        var hasAllowAnonymous = method.GetCustomAttribute<AllowAnonymousAttribute>(inherit: false) is not null;

        (hasAuthorize || hasAllowAnonymous).Should().BeTrue(
            "{0}.{1} must carry an explicit [Authorize(Policy = ...)] or [AllowAnonymous] on the method itself",
            controller, action);
    }

    [Fact]
    public void At_least_one_controller_action_was_discovered()
    {
        // Guards against the enumeration silently finding nothing and the theory vacuously passing.
        ActionMethods().Should().NotBeEmpty();
    }

    private static IEnumerable<MethodInfo> ActionsOf(Type controller)
        => controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName
                && m.GetBaseDefinition() == m
                && m.GetCustomAttribute<NonActionAttribute>() is null);
}
