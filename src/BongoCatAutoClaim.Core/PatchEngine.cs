using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BongoCatAutoClaim.Core;

public sealed class PatchEngine
{
    public void CreatePatchedAssembly(string inputPath, string outputPath, CompatibilityDefinition definition)
    {
        if (definition.PatchDefinition != "shop-timer-ready-buy-v1")
            throw new NotSupportedException($"Unknown patch definition: {definition.PatchDefinition}");
        if (!FileHash.Sha256(inputPath).Equals(definition.OriginalSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Input SHA-256 is not the validated original for this compatibility definition.");

        using (var assembly = ReadAssembly(inputPath))
        {
            var context = GetContext(assembly, definition);
            if (FindAutoBuySequences(context).Count != 0)
                throw new InvalidDataException("The validated original unexpectedly already contains an automatic Buy call.");

            var anchor = FindInsertionPoint(context);
            var il = context.MoveNext.Body.GetILProcessor();
            var loadShop = il.Create(OpCodes.Ldloc_1);
            var loadItem = il.Create(OpCodes.Ldfld, context.ShopItemField);
            var callBuy = il.Create(OpCodes.Callvirt, context.Buy);
            il.InsertAfter(anchor, loadShop);
            il.InsertAfter(loadShop, loadItem);
            il.InsertAfter(loadItem, callBuy);
            WidenTimerGuardBranch(context);
            assembly.Write(outputPath, new WriterParameters { WriteSymbols = false });
        }

        VerifyPatchedAssembly(outputPath, definition);
        var outputHash = FileHash.Sha256(outputPath);
        if (!outputHash.Equals(definition.AcceptedPatchedSha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Patched output was structurally valid but its SHA-256 was not the runtime-accepted hash ({outputHash}).");
    }

    public void VerifyPatchedAssembly(string path, CompatibilityDefinition definition)
    {
        using var assembly = ReadAssembly(path);
        var context = GetContext(assembly, definition);
        if (FindAutoBuySequences(context).Count != 1)
            throw new InvalidDataException("Expected exactly one automatic Buy sequence in TimerUpdate.");
        AssertValidBranches(context.MoveNext);
    }

    public void VerifyOriginalAssembly(string path, CompatibilityDefinition definition)
    {
        using var assembly = ReadAssembly(path);
        var context = GetContext(assembly, definition);
        if (FindAutoBuySequences(context).Count != 0)
            throw new InvalidDataException("Expected no automatic Buy sequence in the original TimerUpdate.");
        AssertValidBranches(context.MoveNext);
    }

    private static AssemblyDefinition ReadAssembly(string path) => AssemblyDefinition.ReadAssembly(path, new ReaderParameters
    {
        InMemory = true,
        ReadSymbols = false
    });

    private static PatchContext GetContext(AssemblyDefinition assembly, CompatibilityDefinition definition)
    {
        if (assembly.MainModule.Mvid != definition.ModuleMvid)
            throw new InvalidDataException($"Module identity mismatch. Expected {definition.ModuleMvid}, found {assembly.MainModule.Mvid}.");

        var shop = Single(assembly.MainModule.Types.Where(type => type.FullName == "BongoCat.Shop"), "BongoCat.Shop type");
        var iterator = Single(shop.NestedTypes.Where(type => type.Name.StartsWith("<TimerUpdate>d__", StringComparison.Ordinal)), "TimerUpdate iterator");
        var moveNext = Single(iterator.Methods.Where(method => method.Name == "MoveNext" && method.HasBody), "TimerUpdate.MoveNext");
        var shopItemField = Single(shop.Fields.Where(field => field.Name == "_shopItem" && field.FieldType.FullName == "BongoCat.ShopItem"), "Shop._shopItem");
        var onChestReady = Single(shop.Methods.Where(method => method.Name == "OnChestReady" && !method.HasParameters), "Shop.OnChestReady");
        var shopItem = Single(assembly.MainModule.Types.Where(type => type.FullName == "BongoCat.ShopItem"), "BongoCat.ShopItem type");
        var buy = Single(shopItem.Methods.Where(method => method.Name == "Buy" && !method.HasParameters), "ShopItem.Buy");
        return new PatchContext(shop, iterator, moveNext, shopItemField, onChestReady, buy);
    }

    private static Instruction FindInsertionPoint(PatchContext context)
    {
        var instructions = context.MoveNext.Body.Instructions;
        var matches = new List<Instruction>();
        for (var i = 4; i < instructions.Count; i++)
        {
            if (instructions[i - 4].OpCode == OpCodes.Ldloc_1
                && instructions[i - 3].OpCode == OpCodes.Ldc_I4_1
                && instructions[i - 2].OpCode == OpCodes.Stfld
                && FullName(instructions[i - 2].Operand) == "System.Boolean BongoCat.Shop::ChestIsReady"
                && instructions[i - 1].OpCode == OpCodes.Ldloc_1
                && instructions[i].OpCode == OpCodes.Call
                && FullName(instructions[i].Operand) == "System.Void BongoCat.Shop::OnChestReady()")
                matches.Add(instructions[i]);
        }
        return Single(matches, "ready-transition insertion point");
    }

    private static IReadOnlyList<Instruction> FindAutoBuySequences(PatchContext context)
    {
        var instructions = context.MoveNext.Body.Instructions;
        var matches = new List<Instruction>();
        for (var i = 2; i < instructions.Count; i++)
        {
            if (instructions[i - 2].OpCode == OpCodes.Ldloc_1
                && instructions[i - 1].OpCode == OpCodes.Ldfld
                && FullName(instructions[i - 1].Operand) == "BongoCat.ShopItem BongoCat.Shop::_shopItem"
                && instructions[i].OpCode == OpCodes.Callvirt
                && FullName(instructions[i].Operand) == "System.Void BongoCat.ShopItem::Buy()")
                matches.Add(instructions[i]);
        }
        return matches;
    }

    private static void WidenTimerGuardBranch(PatchContext context)
    {
        var instructions = context.MoveNext.Body.Instructions;
        var matches = new List<Instruction>();
        for (var i = 4; i < instructions.Count; i++)
        {
            if (instructions[i].OpCode == OpCodes.Bgt_S
                && instructions[i - 1].OpCode == OpCodes.Ldc_I4_0
                && instructions[i - 2].OpCode == OpCodes.Ldfld
                && FullName(instructions[i - 2].Operand) == "System.Int32 BongoCat.Shop::StockRefreshTimeLeft"
                && instructions[i - 3].OpCode == OpCodes.Ldloc_1
                && instructions[i - 4].OpCode == OpCodes.Call
                && FullName(instructions[i - 4].Operand) == "System.Void BongoCat.Shop::UpdateStockRefreshText()")
                matches.Add(instructions[i]);
        }
        Single(matches, "short timer guard branch").OpCode = OpCodes.Bgt;
    }

    private static void AssertValidBranches(MethodDefinition method)
    {
        var instructions = method.Body.Instructions;
        var instructionSet = instructions.ToHashSet();
        foreach (var instruction in instructions)
        {
            if (instruction.OpCode.OperandType is not (OperandType.ShortInlineBrTarget or OperandType.InlineBrTarget))
                continue;
            if (instruction.Operand is not Instruction target || !instructionSet.Contains(target))
                throw new InvalidDataException($"Unresolved branch target at IL_{instruction.Offset:X4}.");
            if (instruction.OpCode.OperandType == OperandType.ShortInlineBrTarget)
            {
                var delta = target.Offset - (instruction.Offset + instruction.GetSize());
                if (delta is < -128 or > 127)
                    throw new InvalidDataException($"Out-of-range short branch at IL_{instruction.Offset:X4}: {delta} bytes.");
            }
        }
    }

    private static string? FullName(object? operand) => operand switch
    {
        MemberReference member => member.FullName,
        _ => operand?.ToString()
    };

    private static T Single<T>(IEnumerable<T> source, string description)
    {
        var items = source.Take(2).ToArray();
        return items.Length == 1 ? items[0] : throw new InvalidDataException($"Expected exactly one {description}; found {items.Length}.");
    }

    private sealed record PatchContext(
        TypeDefinition Shop,
        TypeDefinition Iterator,
        MethodDefinition MoveNext,
        FieldDefinition ShopItemField,
        MethodDefinition OnChestReady,
        MethodDefinition Buy);
}
