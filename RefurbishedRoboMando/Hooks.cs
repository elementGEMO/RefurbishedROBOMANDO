using R2API;
using RoR2;
using UnityEngine;
using MonoMod.Cil;
using Mono.Cecil.Cil;
using System;
using MonoMod.RuntimeDetour;
using RobomandoMod.Survivors.Robomando.SkillStates;
using UnityEngine.Networking;

using static RefurbishedRoboMando.ColorCode;

namespace RefurbishedRoboMando
{
    public class HiddenSkin
    {
        public HiddenSkin()
        {
            LanguageAPI.Add("SECURED_TRANSFORM_2P", "... You've fallen to the influence of the Void.".Style(FontColor.cIsVoid));
            LanguageAPI.Add("SECURED_TRANSFORM", "... {0} has fallen to the influence of the Void.".Style(FontColor.cIsVoid));

            CharacterBody.onBodyInventoryChangedGlobal += ChangeSkin;
        }
        private static void ChangeSkin(CharacterBody self)
        {
            if (NetworkServer.active && self.inventory)
            {
                ModelLocator modelComponent = self.GetComponent<ModelLocator>();
                if (!modelComponent || !modelComponent.modelTransform) return;

                ReplaceCurrentSkin skinLogic = modelComponent.modelTransform.gameObject.GetComponent<ReplaceCurrentSkin>();
                if (!skinLogic) return;

                if (skinLogic)
                {
                    int itemCount = self.inventory.GetItemCount(DLC1Content.Items.BearVoid);
                    SkinDef skin = BodyCatalog.GetBodySkins(self.bodyIndex)[self.skinIndex];
                    if (skin.nameToken.Equals(AssetStatics.skinTokens[1]) && itemCount > 0)
                    {
                        skinLogic.ChangeSkin(AssetStatics.skinTokens[2]);
                        if (!skinLogic.infested)
                        {
                            skinLogic.infested = true;
                            Chat.SendBroadcastChat(new Chat.SubjectFormatChatMessage
                            {
                                subjectAsCharacterBody = self,
                                baseToken = "SECURED_TRANSFORM"
                            });
                        }
                    } else if (skin.nameToken.Equals(AssetStatics.skinTokens[1]) && itemCount <= 0)
                    {
                        skinLogic.infested = false;
                        skinLogic.ChangeSkin(AssetStatics.skinTokens[1]);
                    }
                }
            }
        }
    }
    public class JuryRework
    {
        public JuryRework()
        {
            LanguageAPI.AddOverlay(AssetStatics.tokenPrefix + "KEYWORD_JURY_RIG", "Sabotage".Style(FontColor.cKeywordName) + "Used on a Printer will ".Style(FontColor.cSub) + "activate it for free".Style(FontColor.cIsUtility) + ", but ".Style(FontColor.cSub) + "break it".Style(FontColor.cIsHealth) + ". Costs ".Style(FontColor.cSub) + "X% ".Style(FontColor.cIsHealth) + "(".Style(FontColor.cSub) + "50%" + "/".Style(FontColor.cSub) + "75%".Style(FontColor.cIsHealing) + "/" + "90%".Style(FontColor.cIsHealth) + "/" + "95%".Style(FontColor.cIsDamage) + ") ".Style(FontColor.cSub) + "of your current health".Style(FontColor.cIsHealth) + ".".Style(FontColor.cSub));
            if (!RefurbishConfigEntry.Replace_Tokens.Value) LanguageAPI.AddOverlay(AssetStatics.tokenPrefix + "SPECIAL_HACK_DESCRIPTION", "Jury-Rig".Style(FontColor.cIsUtility) + ". Re-wire a nearby mechanical object, " + "activating it for free".Style(FontColor.cIsUtility) + ".");

            new ILHook(typeof(Hack).GetMethod(nameof(Hack.HackDevice)), StealItem);
        }
        private static void StealItem(ILContext il)
        {
            var cursor = new ILCursor(il);
            var pickupIndex = -1;

            if (cursor.TryGotoNext(
                x => x.MatchStloc(out pickupIndex),
                x => x.MatchLdarg(2),
                x => x.MatchStloc(out _),
                x => x.MatchLdloc(out _),
                x => x.MatchStloc(out _),
                x => x.MatchLdloc(out _),
                x => x.MatchSwitch(out _)
            )) { }
            else Log.Warning("Failed to hook item index");

            if (cursor.TryGotoNext(
                x => x.MatchLdarg(2),
                x => x.MatchStloc(out _),
                x => x.MatchLdloc(out _),
                x => x.MatchStloc(out _),
                x => x.MatchLdloc(out _),
                x => x.MatchSwitch(out _)
            ))
            {
                cursor.Emit(OpCodes.Ldloc, pickupIndex);
                cursor.Emit(OpCodes.Ldarg, 0);
                cursor.Emit(OpCodes.Ldarg, 1);
                cursor.Emit(OpCodes.Ldarg, 2);

                cursor.EmitDelegate<Func<PickupIndex, GameObject, GameObject, Hack.PrinterItemType, PickupIndex>>((self, robo, interact, type) =>
                {
                    var basicIndex = PickupIndex.none;

                    if (type != Hack.PrinterItemType.NONE)
                    {
                        HealthComponent roboHealth = robo.GetComponent<HealthComponent>();
                        ShopTerminalBehavior component = interact.GetComponent<ShopTerminalBehavior>();

                        basicIndex = component.pickupIndex;
                        DamageInfo damageInfo = new()
                        {
                            damageType = DamageType.BypassArmor | DamageType.NonLethal,
                            procCoefficient = 0
                        };

                        if (type == Hack.PrinterItemType.WHITE) damageInfo.damage = roboHealth.fullCombinedHealth * 0.5f;
                        if (type == Hack.PrinterItemType.GREEN) damageInfo.damage = roboHealth.fullCombinedHealth * 0.75f;
                        if (type == Hack.PrinterItemType.RED) damageInfo.damage = roboHealth.fullCombinedHealth * 0.9f;
                        if (type == Hack.PrinterItemType.YELLOW) damageInfo.damage = roboHealth.fullCombinedHealth * 0.95f;

                        roboHealth.TakeDamage(damageInfo);
                    }

                    return basicIndex;
                });
                cursor.Emit(OpCodes.Stloc, pickupIndex);

                var previousIndex = cursor.Index;

                if (cursor.TryGotoNext(
                    x => x.MatchCallOrCallvirt<Vector3>("get_zero"),
                    x => x.MatchStloc(out _)
                ))
                {
                    var skipLabel = cursor.MarkLabel();
                    cursor.Goto(previousIndex);
                    cursor.Emit(OpCodes.Br, skipLabel.Target);
                }
            }
            else
            {
                Log.Warning("Failed to hook scrap replacement");
            }
        }
    }
}