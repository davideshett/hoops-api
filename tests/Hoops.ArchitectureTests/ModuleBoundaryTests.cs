using System.Reflection;
using FluentAssertions;
using NetArchTest.Rules;

namespace Hoops.ArchitectureTests;

/// <summary>
/// Enforces the project reference graph at the type level: module class libraries may depend on the
/// SharedKernel only — never on Infrastructure, never on MVC. The compiler already blocks the
/// project references; this guards against a package reference sneaking a violation in.
/// </summary>
public sealed class ModuleBoundaryTests
{
    private static readonly string[] ModuleAssemblyNames =
    [
        "Hoops.Modules.Identity",
        "Hoops.Modules.Registry",
        "Hoops.Modules.Competitions",
        "Hoops.Modules.GameRecording",
        "Hoops.Modules.Statistics",
    ];

    public static IEnumerable<object[]> ModuleAssemblies =>
        ModuleAssemblyNames.Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(ModuleAssemblies))]
    public void Module_does_not_reference_infrastructure(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);

        var result = Types.InAssembly(assembly)
            .That().ResideInNamespaceStartingWith("Hoops.Modules")
            .ShouldNot().HaveDependencyOn("Hoops.Infrastructure")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "modules must not depend on Infrastructure; offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Theory]
    [MemberData(nameof(ModuleAssemblies))]
    public void Module_does_not_reference_mvc(string assemblyName)
    {
        var assembly = Assembly.Load(assemblyName);

        var result = Types.InAssembly(assembly)
            .That().ResideInNamespaceStartingWith("Hoops.Modules")
            .ShouldNot().HaveDependencyOn("Microsoft.AspNetCore.Mvc")
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            "modules must not depend on MVC; offenders: {0}",
            string.Join(", ", result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Module_assemblies_do_not_reference_infrastructure_assembly()
    {
        // Belt-and-braces at the assembly-reference level, so an empty module can't quietly gain a
        // reference before it has any types for NetArchTest to inspect.
        foreach (var name in ModuleAssemblyNames)
        {
            var referenced = Assembly.Load(name).GetReferencedAssemblies().Select(a => a.Name);
            referenced.Should().NotContain("Hoops.Infrastructure", "module {0} must not reference Infrastructure", name);
        }
    }
}
