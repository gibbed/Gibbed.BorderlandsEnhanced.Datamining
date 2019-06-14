/* Copyright (c) 2019 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Gibbed.Unreflect.Core;
using Newtonsoft.Json;
using Dataminer = BorderlandsEnhancedDatamining.Dataminer;

namespace DumpItems
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            new Dataminer().Run(args, Go);
        }

        private static void Go(Engine engine)
        {
            var itemDefinitionClass = engine.GetClass("WillowGame.ItemDefinition");
            var weaponTypeDefinitionClass = engine.GetClass("WillowGame.WeaponTypeDefinition");
            var inventoryBalanceDefinitionClass = engine.GetClass("WillowGame.InventoryBalanceDefinition");
            var itemPartListCollectionDefinitionClass = engine.GetClass("WillowGame.ItemPartListCollectionDefinition");
            var weaponPartListCollectionDefinitionClass = engine.GetClass("WillowGame.WeaponPartListCollectionDefinition");
            if (itemDefinitionClass == null ||
                weaponTypeDefinitionClass == null ||
                inventoryBalanceDefinitionClass == null ||
                weaponPartListCollectionDefinitionClass == null)
            {
                throw new InvalidOperationException();
            }

            var itemBalances = new List<dynamic>();
            var weaponTypeBalances = new List<dynamic>();

            {
                var balanceDefinitions = engine.Objects
                    .Where(o => o.IsA(inventoryBalanceDefinitionClass) &&
                           o.GetName().StartsWith("Default__") == false)
                    .OrderBy(o => o.GetPath());
                foreach (dynamic balanceDefinition in balanceDefinitions)
                {
                    dynamic associatedDefinition = null;

                    if (balanceDefinition.InventoryDefinition != null)
                    {
                        associatedDefinition = balanceDefinition.InventoryDefinition;
                        var partListCollection = balanceDefinition.PartListCollection;
                        if (partListCollection != null)
                        {
                            if (partListCollection.IsA(itemPartListCollectionDefinitionClass) == true)
                            {
                                if (partListCollection.AssociatedItem != associatedDefinition)
                                {
                                    throw new InvalidOperationException();
                                }
                            }
                            else if (partListCollection.IsA(weaponPartListCollectionDefinitionClass) == true)
                            {
                                if (partListCollection.AssociatedWeaponType != associatedDefinition)
                                {
                                    throw new InvalidOperationException();
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Bad definition (part list mismatch): {balanceDefinition.GetPath()}");
                                continue;
                            }
                        }
                    }
                    else
                    {
                        var partListCollection = balanceDefinition.PartListCollection;
                        if (partListCollection != null)
                        {
                            if (partListCollection.IsA(itemPartListCollectionDefinitionClass) == true)
                            {
                                associatedDefinition = partListCollection.AssociatedItem;
                            }
                            else if (partListCollection.IsA(weaponPartListCollectionDefinitionClass) == true)
                            {
                                associatedDefinition = partListCollection.AssociatedWeaponType;
                            }
                            else
                            {
                                throw new NotSupportedException();
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Bad definition (associated definition missing): {balanceDefinition.GetPath()}");
                            continue;
                        }
                    }

                    if (associatedDefinition.IsA(itemDefinitionClass) == true)
                    {
                        itemBalances.Add(associatedDefinition);
                    }
                    else if (associatedDefinition.IsA(weaponTypeDefinitionClass) == true)
                    {
                        weaponTypeBalances.Add(associatedDefinition);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
            }

            using (var output = Dataminer.NewDump("Weapon Types.json"))
            using (var writer = new JsonTextWriter(output))
            {
                writer.Indentation = 2;
                writer.IndentChar = ' ';
                writer.Formatting = Formatting.Indented;

                writer.WriteStartObject();
                foreach (var weaponType in weaponTypeBalances.Distinct().OrderBy(wp => wp.GetPath()))
                {
                    writer.WritePropertyName(weaponType.GetPath());
                    writer.WriteStartObject();

                    UnrealClass weaponPartClass = weaponType.GetClass();
                    if (weaponPartClass.Path != "WillowGame.WeaponTypeDefinition")
                    {
                        throw new InvalidOperationException();
                    }

                    var weaponBaseType = GetWeaponType(weaponType);

                    writer.WritePropertyName("type");
                    writer.WriteValue(((WeaponType)weaponBaseType).ToString());

                    writer.WritePropertyName("name");
                    writer.WriteValue(weaponType.TypeName);

                    if (weaponType.TitleList != null &&
                        weaponType.TitleList.Length > 0)
                    {
                        writer.WritePropertyName("titles");
                        writer.WriteStartArray();
                        IEnumerable<dynamic> titleList = weaponType.TitleList;
                        foreach (var title in titleList
                            .Where(tp => tp != null)
                            .OrderBy(tp => tp.GetPath()))
                        {
                            writer.WriteValue(title.GetPath());
                        }
                        writer.WriteEndArray();
                    }

                    if (weaponType.PrefixList != null &&
                        weaponType.PrefixList.Length > 0)
                    {
                        writer.WritePropertyName("prefixes");
                        writer.WriteStartArray();
                        IEnumerable<dynamic> prefixList = weaponType.PrefixList;
                        foreach (var prefix in prefixList
                            .Where(pp => pp != null)
                            .OrderBy(pp => pp.GetPath()))
                        {
                            writer.WriteValue(prefix.GetPath());
                        }
                        writer.WriteEndArray();
                    }

                    DumpCustomPartTypeData(writer, "body_parts", weaponType.BodyParts);
                    DumpCustomPartTypeData(writer, "grip_parts", weaponType.GripParts);
                    DumpCustomPartTypeData(writer, "magazine_parts", weaponType.MagazineParts);
                    DumpCustomPartTypeData(writer, "barrel_parts", weaponType.BarrelParts);
                    DumpCustomPartTypeData(writer, "sight_parts", weaponType.SightParts);
                    DumpCustomPartTypeData(writer, "stock_parts", weaponType.StockParts);
                    DumpCustomPartTypeData(writer, "action_parts", weaponType.ActionParts);
                    DumpCustomPartTypeData(writer, "accessory_parts", weaponType.AccessoryParts);
                    DumpCustomPartTypeData(writer, "material_parts", weaponType.MaterialParts);

                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }

            using (var output = Dataminer.NewDump("Item Types.json"))
            using (var writer = new JsonTextWriter(output))
            {
                writer.Indentation = 2;
                writer.IndentChar = ' ';
                writer.Formatting = Formatting.Indented;

                writer.WriteStartObject();
                foreach (var itemType in itemBalances.Distinct().OrderBy(wp => wp.GetPath()))
                {
                    writer.WritePropertyName(itemType.GetPath());
                    writer.WriteStartObject();

                    UnrealClass itemPartClass = itemType.GetClass();
                    if (itemPartClass.Path != "WillowGame.ItemDefinition")
                    {
                        throw new InvalidOperationException();
                    }

                    if (string.IsNullOrEmpty((string)itemType.ItemName) == false)
                    {
                        writer.WritePropertyName("name");
                        writer.WriteValue(itemType.ItemName);
                    }

                    if ((bool)itemType.bItemNameIsFullName == true)
                    {
                        writer.WritePropertyName("has_full_name");
                        writer.WriteValue(true);
                    }

                    if ((bool)itemType.bMissionItem == true)
                    {
                        writer.WritePropertyName("is_mission_item");
                        writer.WriteValue(true);
                    }

                    var characterRequirement = (CharacterRequirement)itemType.RequiredCharacter;
                    if (characterRequirement != CharacterRequirement.None)
                    {
                        writer.WritePropertyName("character_required");
                        writer.WriteValue(characterRequirement.ToString());
                    }

                    if (itemType.TitleList != null &&
                        itemType.TitleList.Length > 0)
                    {
                        writer.WritePropertyName("titles");
                        writer.WriteStartArray();
                        IEnumerable<dynamic> titleList = itemType.TitleList;
                        foreach (var title in titleList
                            .Where(tp => tp != null)
                            .OrderBy(tp => tp.GetPath()))
                        {
                            writer.WriteValue(title.GetPath());
                        }
                        writer.WriteEndArray();
                    }

                    if (itemType.PrefixList != null &&
                        itemType.PrefixList.Length > 0)
                    {
                        writer.WritePropertyName("prefixes");
                        writer.WriteStartArray();
                        IEnumerable<dynamic> prefixList = itemType.PrefixList;
                        foreach (var prefix in prefixList
                            .Where(pp => pp != null)
                            .OrderBy(pp => pp.GetPath()))
                        {
                            writer.WriteValue(prefix.GetPath());
                        }
                        writer.WriteEndArray();
                    }

                    DumpCustomPartTypeData(writer, "body_parts", itemType.BodyParts);
                    DumpCustomPartTypeData(writer, "left_side_parts", itemType.LeftSideParts);
                    DumpCustomPartTypeData(writer, "right_side_parts", itemType.RightSideParts);
                    DumpCustomPartTypeData(writer, "material_parts", itemType.MaterialParts);

                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
            }
        }

        private static WeaponType GetWeaponType(dynamic type)
        {
            bool GetFlag(ref int counter, bool value)
            {
                if (value == true)
                {
                    counter++;
                }
                return value;
            }

            int count = 0;

            var isPistol = GetFlag(ref count, type.bPistol);
            var isShotgun = GetFlag(ref count, type.bShotgun);
            var isSMG = GetFlag(ref count, type.bSMG);
            var isSniperRifle = GetFlag(ref count, type.bSniperRifle);
            var isBlade = GetFlag(ref count, type.bBlade);
            var isDiscus = GetFlag(ref count, type.bDiscus);
            var isRocketLauncher = GetFlag(ref count, type.bRocketLauncher);
            var isGrenadeLauncher = GetFlag(ref count, type.bGrenadeLauncher);
            var isPlasmaWeapon = GetFlag(ref count, type.bPlasmaWeapon);
            var isAssaultRifle = GetFlag(ref count, type.bAssaultRifle);
            var isHeavyWeapon = GetFlag(ref count, type.bHeavyWeapon);
            var isExtraWeapon1 = GetFlag(ref count, type.bExtraWeapon1);
            var isExtraWeapon2 = GetFlag(ref count, type.bExtraWeapon2);
            var isExtraWeapon3 = GetFlag(ref count, type.bExtraWeapon3);
            var isExtraWeapon4 = GetFlag(ref count, type.bExtraWeapon4);

            if (count == 0 || count > 1)
            {
                throw new InvalidOperationException();
            }

            if (isPistol == true) return WeaponType.Pistol;
            if (isShotgun == true) return WeaponType.Shotgun;
            if (isSMG == true) return WeaponType.SMG;
            if (isSniperRifle == true) return WeaponType.SniperRifle;
            if (isBlade == true) return WeaponType.Blade;
            if (isDiscus == true) return WeaponType.Discus;
            if (isRocketLauncher == true) return WeaponType.RocketLauncher;
            if (isGrenadeLauncher == true) return WeaponType.GrenadeLauncher;
            if (isPlasmaWeapon == true) return WeaponType.PlasmaWeapon;
            if (isAssaultRifle == true) return WeaponType.AssaultRifle;
            if (isHeavyWeapon == true) return WeaponType.HeavyWeapon;
            if (isExtraWeapon1 == true) return WeaponType.ExtraWeapon1;
            if (isExtraWeapon2 == true) return WeaponType.ExtraWeapon2;
            if (isExtraWeapon3 == true) return WeaponType.ExtraWeapon3;
            if (isExtraWeapon4 == true) return WeaponType.ExtraWeapon4;
            throw new NotSupportedException();
        }

        private static void DumpCustomPartTypeData(JsonWriter writer, string name, dynamic customPartTypeData)
        {
            if (customPartTypeData != null)
            {
                var weightedParts = ((IEnumerable<dynamic>)customPartTypeData.WeightedParts).ToArray();
                if (weightedParts.Length > 0)
                {
                    writer.WritePropertyName(name);
                    writer.WriteStartArray();

                    foreach (var weightedPart in weightedParts
                        .Where(wp => wp.Part != null)
                        .OrderBy(wp => wp.Part.GetPath()))
                    {
                        writer.WriteValue(weightedPart.Part.GetPath());
                    }

                    writer.WriteEndArray();
                }
            }
        }
    }
}
