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
using System.IO;
using System.Linq;
using System.Text;
using Gibbed.Unreflect.Core;
using Newtonsoft.Json;

namespace DumpPlayerClasses
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            new BorderlandsEnhancedDatamining.Dataminer().Run(args, Go);
        }

        private static void Go(Engine engine)
        {
            var playerClassDefinitionClass = engine.GetClass("WillowGame.PlayerClassDefinition");
            if (playerClassDefinitionClass == null)
            {
                throw new InvalidOperationException();
            }

            Directory.CreateDirectory("dumps");

            using (var output = new StreamWriter(Path.Combine("dumps", "Player Classes.json"), false, Encoding.Unicode))
            using (var writer = new JsonTextWriter(output))
            {
                writer.Indentation = 2;
                writer.IndentChar = ' ';
                writer.Formatting = Formatting.Indented;

                writer.WriteStartObject();

                var playerClassDefinitionClasses = engine.Objects
                    .Where(o => o.IsA(playerClassDefinitionClass) &&
                                o.GetName().StartsWith("Default__") ==
                                false)
                    .OrderBy(o => o.GetPath());
                foreach (dynamic playerClassDefinition in playerClassDefinitionClasses)
                {
                    writer.WritePropertyName(playerClassDefinition.GetPath());
                    writer.WriteStartObject();

                    var characterName = (CharacterNames)playerClassDefinition.CharacterName;

                    writer.WritePropertyName("name");
                    writer.WriteValue(characterName.ToString());

                    // TODO: PlayerSkillSet

                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
                writer.Flush();
            }
        }
    }
}
