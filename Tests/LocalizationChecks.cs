using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;

static class LocalizationChecks
{
    static IEnumerable<TypeDefinition> Types(IEnumerable<TypeDefinition> types)
    {
        foreach (var type in types)
        {
            yield return type;
            foreach (var nested in Types(type.NestedTypes)) yield return nested;
        }
    }

    static string Body(MethodDefinition method)
    {
        var body = method.Body;
        Func<object, string> operand = value => {
            if (value == null) return "";
            var target = value as Instruction;
            if (target != null) return "@" + body.Instructions.IndexOf(target);
            var targets = value as Instruction[];
            if (targets != null) return string.Join(",", targets.Select(t => body.Instructions.IndexOf(t).ToString()));
            var variable = value as VariableDefinition;
            if (variable != null) return variable.Index + ":" + variable.VariableType.FullName;
            var parameter = value as ParameterDefinition;
            if (parameter != null) return parameter.Index + ":" + parameter.ParameterType.FullName;
            var member = value as MemberReference;
            if (member != null) return Regex.Replace(member.FullName, @"(b__|<>9__)\d+_", "$1N_");
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        };
        return string.Join(";", body.Variables.Select(v => v.VariableType.FullName)) + "\n"
            + string.Join("\n", body.Instructions.Select(i => i.OpCode + " " + operand(i.Operand))) + "\n"
            + string.Join("\n", body.ExceptionHandlers.Select(h => h.HandlerType + ":"
                + operand(h.TryStart) + ":" + operand(h.TryEnd) + ":"
                + operand(h.HandlerStart) + ":" + operand(h.HandlerEnd)));
    }

    static int Main(string[] args)
    {
        var root = args[0];
        var before = AssemblyDefinition.ReadAssembly(Path.Combine(root, "build", "baseline-plugins", "ThrottleControlledAvionics.dll"));
        var after = AssemblyDefinition.ReadAssembly(Path.Combine(root, "build", "Plugins", "ThrottleControlledAvionics.dll"));
        {
            var originalTypes = Types(before.MainModule.Types).ToArray();
            var currentTypes = Types(after.MainModule.Types).ToDictionary(t => t.FullName);
            var checkedCount = 0;
            var failures = new List<string>();
            foreach (var originalType in originalTypes)
            {
                if (originalType.Name.Contains("<")) continue;
                TypeDefinition currentType;
                if (!currentTypes.TryGetValue(originalType.FullName, out currentType))
                {
                    failures.Add("Removed type: " + originalType.FullName);
                    continue;
                }
                foreach (var field in originalType.Fields.Where(f => f.IsPublic || f.IsFamily || f.IsFamilyOrAssembly))
                    if (!currentType.Fields.Any(f => f.FullName == field.FullName))
                        failures.Add("Removed public or protected field: " + field.FullName);
                foreach (var original in originalType.Methods)
                {
                    if (!original.HasBody || original.Name.Contains("<") || original.Name == ".cctor") continue;
                    var current = currentType.Methods.FirstOrDefault(m => m.FullName == original.FullName);
                    if (current == null) { failures.Add("Removed method: " + original.FullName); continue; }
                    if (original.Name.StartsWith("Draw") || original.Name.StartsWith("draw_")
                        || original.Name == "OnRenderObject" || originalType.Name.StartsWith("ControlPanel")
                        || originalType.Name == "TCAManual"
                        || originalType.Name == "TCAComponent" && original.Name == "Message"
                        || originalType.Name == "TCAGui" && original.Name == "Status") continue;
                    // Text-producing methods are reviewed in the source diff; numerical methods must match exactly.
                    if (original.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr)) continue;
                    if (current.Body.Instructions.Any(i => i.Operand is MethodReference
                        && ((MethodReference)i.Operand).DeclaringType.Name == "Loc")) continue;
                    if (Body(original) != Body(current)) failures.Add("Changed non-text IL: " + original.FullName);
                    else checkedCount++;
                }
            }
            var oldNavigation = originalTypes.Single(t => t.Name == "NavigationTab").Methods.Single(m => m.Name == "waypoint_editor");
            var newNavigation = currentTypes[oldNavigation.DeclaringType.FullName].Methods.Single(m => m.Name == "waypoint_editor");
            foreach (var fieldName in new[] { "LatField", "LonField", "AltField" })
            {
                Func<MethodDefinition, int> references = m => m.Body.Instructions.Count(i => i.Operand is FieldReference
                    && ((FieldReference)i.Operand).Name == fieldName);
                if (references(oldNavigation) != references(newNavigation))
                    failures.Add("Waypoint editor coordinate binding changed: " + fieldName);
            }
            foreach (var typeName in new[] { "EngineOptimizer", "RCSOptimizer", "EngineWrapper", "VesselWrapper", "TimeWarpControl" })
            {
                var oldType = originalTypes.Single(t => t.Name == typeName);
                var newType = currentTypes[oldType.FullName];
                foreach (var method in oldType.Methods.Where(m => m.HasBody && !m.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldstr)))
                {
                    var other = newType.Methods.Single(m => m.FullName == method.FullName);
                    if (Body(method) != Body(other)) failures.Add("Core algorithm changed: " + method.FullName);
                }
            }
            foreach (var failure in failures) Console.WriteLine("FAIL " + failure);
            Console.WriteLine("Compared " + checkedCount + " unchanged non-text methods against a fresh v3.8.0.1 build.");
            return failures.Count == 0 ? 0 : 1;
        }
    }
}
