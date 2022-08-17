using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;

namespace SynFixWeaponAttackSpeed
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            return await SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
                .SetTypicalOpen(GameRelease.SkyrimSE, "SynFixWeaponAttackSpeed.esp")
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {

            Console.WriteLine($"Pstch spell magic effects magnitude.");
            int patchedSpellEffects = 0;
            foreach (var spellGetter in state.LoadOrder.PriorityOrder.Spell().WinningOverrides())
            {
                if (spellGetter.IsDeleted) continue;
                if (spellGetter.Effects == null) continue;

                Spell? spell = null;
                int effectIndex = -1;
                foreach (var effectGetter in spellGetter.Effects)
                {
                    effectIndex++;
                    if (effectGetter.BaseEffect.IsNull) continue;
                    if (effectGetter.Data == null || effectGetter.Data.Magnitude <= 1) continue; // we fix only when 1+
                    if (!effectGetter.BaseEffect.TryResolve(state.LinkCache, out var magicEffectGetter)) continue;

                    if (magicEffectGetter.Archetype is not IMagicEffectEnhanceWeaponArchetypeGetter a) continue;
                    if (a.ActorValue != ActorValue.WeaponSpeedMult) continue;

                    if (spell == null) spell = state.PatchMod.Spells.GetOrAddAsOverride(spellGetter);

                    var effectData = spell.Effects[effectIndex].Data!;
                    var fixedMagnitude = (float)effectData.Magnitude - (float)1.0;
                    Console.WriteLine($"Fix magic effect {spellGetter.FormKey.ModKey}/{spellGetter.EditorID}/{magicEffectGetter.EditorID}, magnitude: {effectData.Magnitude}, new: {fixedMagnitude.ToString("0.##")}");
                    effectData.Magnitude = fixedMagnitude;

                    patchedSpellEffects++;
                }
            }

            Console.WriteLine($"Pstched {patchedSpellEffects} magic effects.");

            Console.WriteLine($"Add ability to all races to set base weapon speed mult value to 1.0..");
            // add effects
            var FWSAbilitySpellEffect = state.PatchMod.MagicEffects.AddNew("SynFixWeaponSpeedMultAbilityEffectR");
            FWSAbilitySpellEffect.Flags = MagicEffect.Flag.Recover | MagicEffect.Flag.NoHitEffect | MagicEffect.Flag.Painless | MagicEffect.Flag.NoArea | MagicEffect.Flag.HideInUI;
            FWSAbilitySpellEffect.BaseCost = 0;
            FWSAbilitySpellEffect.Archetype = new MagicEffectPeakValueModArchetype
            {
                ActorValue = ActorValue.WeaponSpeedMult
            };
            FWSAbilitySpellEffect.CastType = CastType.ConstantEffect;
            FWSAbilitySpellEffect.TargetType = TargetType.Self;

            var FWSAbilitySpellEffectL = state.PatchMod.MagicEffects.AddNew("SynFixWeaponSpeedMultAbilityEffectL");
            FWSAbilitySpellEffectL.Flags = MagicEffect.Flag.Recover | MagicEffect.Flag.NoHitEffect | MagicEffect.Flag.Painless | MagicEffect.Flag.NoArea | MagicEffect.Flag.HideInUI;
            FWSAbilitySpellEffectL.BaseCost = 0;
            FWSAbilitySpellEffectL.Archetype = new MagicEffectPeakValueModArchetype
            {
                ActorValue = ActorValue.LeftWeaponSpeedMultiply
            };
            FWSAbilitySpellEffectL.CastType = CastType.ConstantEffect;
            FWSAbilitySpellEffectL.TargetType = TargetType.Self;

            // add spell
            var FWSAbilitySpell = state.PatchMod.Spells.AddNew("SynFixWeaponSpeedMultAbility");
            FWSAbilitySpell.BaseCost = 0;
            FWSAbilitySpell.Type = SpellType.Ability;
            FWSAbilitySpell.CastType = CastType.ConstantEffect;
            FWSAbilitySpell.TargetType = TargetType.Self;
            FWSAbilitySpell.Flags = SpellDataFlag.ManualCostCalc | SpellDataFlag.IgnoreResistance | SpellDataFlag.NoAbsorbOrReflect;

            var e = new Effect();
            e.BaseEffect.FormKey = FWSAbilitySpellEffect.FormKey;
            e.Data = new EffectData
            {
                Magnitude = 1
            };
            var eL = new Effect();
            eL.BaseEffect.FormKey = FWSAbilitySpellEffectL.FormKey;
            eL.Data = new EffectData
            {
                Magnitude = 1
            };

            FWSAbilitySpell.Effects.Add(e);
            FWSAbilitySpell.Effects.Add(eL);

            int patchedRacesCount = 0;
            foreach (var raceGetter in state.LoadOrder.PriorityOrder.Race().WinningOverrides())
            {
                var patchedRace = state.PatchMod.Races.GetOrAddAsOverride(raceGetter);

                if (patchedRace.ActorEffect == null) patchedRace.ActorEffect = new Noggog.ExtendedList<Mutagen.Bethesda.Plugins.IFormLinkGetter<ISpellRecordGetter>>();

                patchedRace.ActorEffect.Add(FWSAbilitySpell);

                patchedRacesCount++;
            }

            Console.WriteLine($"Added fix ability to {patchedRacesCount} races.");
        }
    }
}
