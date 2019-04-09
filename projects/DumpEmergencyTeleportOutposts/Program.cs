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
using System.IO;
using System.Linq;
using System.Text;
using Gibbed.Unreflect.Core;
using Newtonsoft.Json;

namespace DumpTravelStations
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            new BorderlandsEnhancedDatamining.Dataminer().Run(args, Go);
        }

        private static void Go(Engine engine)
        {
            dynamic outpostLookupClass = engine.GetClass("WillowGame.EmergencyTeleportOutpostLookup");
            dynamic willowGlobalsClass = engine.GetClass("WillowGame.WillowGlobals");
            dynamic dlcPackageClass = engine.GetClass("WillowGame.DLCPackageDefinition");
            if (outpostLookupClass == null ||
                willowGlobalsClass == null ||
                dlcPackageClass == null)
            {
                throw new InvalidOperationException();
            }

            Directory.CreateDirectory("dumps");

            var sourceLookup = new Dictionary<string, string>();
            dynamic willowGlobals = engine.Objects.Single(
                o => o.IsA(willowGlobalsClass) == true &&
                o.GetName().StartsWith("Default__") == false);

            dynamic masterLookup = willowGlobals.MasterRegistrationStationList;

            var outpostOrder = new List<string>();
            foreach (dynamic lookupObject in masterLookup.OutpostLookupList)
            {
                outpostOrder.Add(lookupObject.OutpostName);
            }

            foreach (dynamic dlcPackage in engine.Objects.Where(
                o => o.IsA(dlcPackageClass) == true &&
                o.GetName().StartsWith("Default__") == false))
            {
                sourceLookup.Add(dlcPackage.TeleportLookupObject.GetPath(), dlcPackage.GetPath());
            }

            using (var output = new StreamWriter(Path.Combine("dumps", "Emergency Teleport Outposts.json"), false, Encoding.Unicode))
            using (var writer = new JsonTextWriter(output))
            {
                writer.Indentation = 2;
                writer.IndentChar = ' ';
                writer.Formatting = Formatting.Indented;

                var outpostNames = new List<string>();

                writer.WriteStartObject();
                foreach (dynamic lookup in engine.Objects
                    .Where(
                        o => o.IsA(outpostLookupClass) &&
                             o.GetName().StartsWith("Default__") == false)
                    .OrderBy(o => o.GetPath())
                    .Distinct())
                {
                    // The master list has every single station, we don't want to write it too.
                    if (lookup == masterLookup)
                    {
                        continue;
                    }

                    writer.WritePropertyName(lookup.GetPath());
                    writer.WriteStartObject();

                    string source;
                    if (sourceLookup.TryGetValue((string)lookup.GetPath(), out source) == true &&
                        source != "None")
                    {
                        writer.WritePropertyName("dlc_package");
                        writer.WriteValue(source);
                    }

                    writer.WritePropertyName("outposts");
                    writer.WriteStartArray();
                    foreach (dynamic lookupObject in ((UnrealObject[])lookup.OutpostLookupList)
                        .Cast<dynamic>()
                        .OrderBy(l => outpostOrder.IndexOf(l.OutpostName)))
                    {
                        var outpostName = (string)lookupObject.OutpostName;
                        if (outpostNames.Contains(outpostName) == true)
                        {
                            Console.WriteLine($"Skipping duplicate outpost {outpostName}.");
                            continue;
                        }
                        outpostNames.Add(outpostName);

                        writer.WriteStartObject();

                        writer.WritePropertyName("name");
                        writer.WriteValue(outpostName);

                        writer.WritePropertyName("path");
                        writer.WriteValue(lookupObject.OutpostPathName);

                        var isInitiallyActive = (bool)lookupObject.bInitiallyActive;
                        if (isInitiallyActive == true)
                        {
                            writer.WritePropertyName("is_initially_active");
                            writer.WriteValue(true);
                        }

                        var isCheckpointOnly = (bool)lookupObject.bCheckpointOnly;
                        if (isCheckpointOnly == true)
                        {
                            writer.WritePropertyName("is_checkpoint_only");
                            writer.WriteValue(true);
                        }

                        var displayName = (string)lookupObject.OutpostDisplayName;
                        if (string.IsNullOrEmpty(displayName) == false)
                        {
                            writer.WritePropertyName("display_name");
                            writer.WriteValue(displayName);
                        }

                        var description = (string)lookupObject.OutpostDescription;
                        if (string.IsNullOrEmpty(description) == false &&
                            description != "No Description")
                        {
                            writer.WritePropertyName("description");
                            writer.WriteValue(description);
                        }

                        var previousOutpost = (string)lookupObject.PreviousOutpost;
                        if (string.IsNullOrEmpty(previousOutpost) == false &&
                            previousOutpost != "None")
                        {
                            writer.WritePropertyName("previous_outpost");
                            writer.WriteValue(previousOutpost);
                        }

                        if (lookupObject.MissionDependencies != null &&
                            lookupObject.MissionDependencies.Length > 0)
                        {
                            writer.WritePropertyName("mission_dependencies");
                            writer.WriteStartObject();
                            foreach (var missionDependency in lookupObject.MissionDependencies)
                            {
                                writer.WritePropertyName(missionDependency.MissionDefinition.GetPath());
                                writer.WriteValue(((MissionStatus)missionDependency.MissionStatus).ToString());
                            }
                            writer.WriteEndObject();
                        }

                        var outpostIndex = outpostOrder.IndexOf(outpostName);
                        if (outpostIndex < 0)
                        {
                            throw new InvalidOperationException();
                        }

                        writer.WritePropertyName("sort_order");
                        writer.WriteValue(outpostIndex);

                        writer.WriteEndObject();
                    }
                    writer.WriteEndArray();

                    writer.WriteEndObject();
                }
                writer.WriteEndObject();
                writer.Flush();
            }
        }
    }
}
