using System.Reflection;
using FluentAssertions;
using Hoops.Modules.GameRecording.Domain.Projection;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Hoops.ArchitectureTests;

/// <summary>
/// ADR-001 / §9.1: the projector is a PURE function. No I/O, no ambient time, no randomness — identical
/// input must always produce identical output, because full replay is the correctness guarantee for
/// every statistic the product sells. This test reads the compiled IL, so it cannot be talked around.
/// </summary>
public sealed class ProjectorPurityTests
{
    private static readonly string[] ForbiddenMembers =
    [
        "System.DateTime::get_Now",
        "System.DateTime::get_UtcNow",
        "System.DateTimeOffset::get_Now",
        "System.DateTimeOffset::get_UtcNow",
        "System.Guid::NewGuid",
        "System.Guid::CreateVersion7",
    ];

    private static readonly string[] ForbiddenTypes =
    [
        "Random",
        "DbContext",
        "HttpClient",
        "File",
        "Directory",
    ];

    [Fact]
    public void The_projector_calls_no_ambient_time_or_randomness()
    {
        var offenders = new List<string>();

        foreach (var (method, instruction) in ProjectorInstructions())
        {
            if (instruction.Operand is MethodReference callee)
            {
                var signature = $"{callee.DeclaringType.FullName}::{callee.Name}";
                if (ForbiddenMembers.Any(f => signature.EndsWith(f, StringComparison.Ordinal)))
                {
                    offenders.Add($"{method.Name} calls {signature}");
                }

                if (ForbiddenTypes.Any(t => callee.DeclaringType.Name == t))
                {
                    offenders.Add($"{method.Name} uses {callee.DeclaringType.FullName}");
                }
            }
        }

        offenders.Should().BeEmpty("the projector must be pure; offenders: {0}", string.Join("; ", offenders));
    }

    [Fact]
    public void The_projection_namespace_references_no_persistence_or_mvc_types()
    {
        var assembly = typeof(GameProjector).Assembly;

        var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();

        referenced.Should().NotContain("Microsoft.EntityFrameworkCore");
        referenced.Should().NotContain("Hoops.Infrastructure");
        referenced.Should().NotContain("Microsoft.AspNetCore.Mvc.Core");
    }

    [Fact]
    public void The_projector_produces_identical_output_for_identical_input()
    {
        // A behavioural companion to the IL check: determinism is the property purity buys us.
        var context = new GameContext(
            Hoops.SharedKernel.Identifiers.GameId.New(),
            Hoops.SharedKernel.Identifiers.CompetitionTeamId.New(),
            Hoops.SharedKernel.Identifiers.CompetitionTeamId.New(),
            [],
            Hoops.SharedKernel.RuleSet.Fiba());

        var projector = new GameProjector();
        var first = projector.Project(context, []);
        var second = projector.Project(context, []);

        second.Should().BeEquivalentTo(first);
    }

    private static IEnumerable<(MethodDefinition Method, Instruction Instruction)> ProjectorInstructions()
    {
        var path = typeof(GameProjector).Assembly.Location;
        using var module = ModuleDefinition.ReadModule(path);

        var types = module.Types
            .Where(t => t.Namespace?.StartsWith("Hoops.Modules.GameRecording.Domain.Projection", StringComparison.Ordinal) == true)
            .SelectMany(Flatten);

        foreach (var type in types)
        {
            foreach (var method in type.Methods.Where(m => m.HasBody))
            {
                foreach (var instruction in method.Body.Instructions)
                {
                    yield return (method, instruction);
                }
            }
        }
    }

    private static IEnumerable<TypeDefinition> Flatten(TypeDefinition type)
    {
        yield return type;
        foreach (var nested in type.NestedTypes.SelectMany(Flatten))
        {
            yield return nested;
        }
    }
}
