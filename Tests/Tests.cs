using Vintagestory.API.Common;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace VintageStory.ModPeek.Tests;

[TestClass]
public class Tests
{
	readonly Dictionary<string, ModInfo> _referenceInfos = new() {
		["Bee_Keeper_v1.1.0.zip"] = new ModInfo() {
			Type             = EnumModType.Content,
			ModID            = "beekeepernew",
			Name             = "Bee Keeper",
			Description      = "Adds a beekeeper class",
			Version          = "1.1.0",
			Authors          = [ "The Unnamed System", "ComitatensSaxoni" ],
			Side             = EnumAppSide.Universal,
			RequiredOnClient = true,
		},
		["jtraits_0.2.6.zip"] = new ModInfo() {
			Type         = EnumModType.Content,
			ModID        = "jtraits",
			Name         = "jtraits",
			Authors      = [ "Joeyfar624" ],
			Description  = "MODIFIES ALL THE VANILLA TRAITS",
			Version      = null, // malformed '0.2.6 ', trailing space
			Dependencies = [ new("game", null) ],
		},
		["Recycle Metalwörk_v1.0.3.zip"] = new ModInfo() {
			Type        = EnumModType.Content,
			ModID       = "recyclemetalwork",
			Name        = " recyclemetalwork",
			Authors     = [ "Tomori0" ],
			Description = "Added quick recycle recipes for already installed Shanks on metalwork, metal objects, ruined weapons, and scrap weapons.",
			Version     = "1.0.3",
		},
		["StepUp-v1.2.0.cs"] = new ModInfo() {
			Type        = EnumModType.Code,
			Side        = EnumAppSide.Client,
			ModID       = "stepup",
			Name        = "StepUp",
			Authors     = [ "copygirl" ],
			Description = "Doubles players' step height to allow stepping up full blocks",
			Version     = "1.2.0",
			Website     = "https://www.vintagestory.at/forums/topic/3349-stepup-v120/",
			Dependencies = [ new("game", null) ],
		},
		["tailoringforall.zip"] = new ModInfo() {
			Type         = EnumModType.Content,
			ModID        = "tailoringforall",
			Name         = "Tailoring for All",
			Authors      = [ "BluntTongs" ],
			Description  = "Adds more expensive versions of Tailor-exclusive recipes for all classes to use",
			Version      = "1.0.1",
			Dependencies = [ new("game", null) ],
		},
		["TexturePack and CloneTool 1.0.4.zip"] = new ModInfo() {
			Type         = EnumModType.Content,
			ModID        = "clonetoolforsica",
			Name         = "Texture improvements for CloneTool in SICA.",
			Authors      = [ "Sirayne" ],
			Description  = "Slightly changes the textures used by the CloningTool in Sirayne’s Infinite Clone Area (SICA). \r\nOriginally made for personal use on the BetterRuins builders server.",
			Side         = EnumAppSide.Client,
			Version      = "1.0.4",
			TextureSize  =  32,
			//Dependencies = [ new("game", "1.20.9") ], // malformed, wrong property name (dependency instead of dependencies)
			Website      = "https://mods.vintagestory.at/clonetoolforsica",
		},
		["warriordrink_v1.0.2.zip"] = new ModInfo() {
			Type         = EnumModType.Content,
			Name         = "A Warrior's Drink! ",
			ModID        = "warriordrink",
			Side         = EnumAppSide.Server,
			Description  = "Make traditional Mongolian Nomad alcohol from milk.  Transform curdled milk into Airag in a barrel, distill into Arkhi Liquor.  Grants dairy nutrition.",
			//Authors      = [ "Flint_N_Steel" ], // malformed, wrong property name (author instead of authors)
			Version      = "1.0.2",
			//Dependencies = [ new("game", "1.19.0") ], // malformed, wrong property name (gameversions instead of dependencies)
			Website      = "http://wiki.vintagestory.at/",
		},
		["Waxpress_1.0.0.zip"] = new ModInfo() {
			Type         = EnumModType.Content,
			ModID        = "waxpress", // generated from name
			Name         = "Waxpress",
			Authors      = ["Korhaka"],
			Description  = "Press wax in the fruit press",
			Version      = "1.0.0",
			Dependencies = [ new("game", "1.19.8") ],
		},
		["WeaponPackAlphaUnofficial_1.6.0.zip"] = new ModInfo() {
			Type             = EnumModType.Code,
			ModID            = "weaponpackalphaunoff",
			Name             = "Alpha Weapon Pack unofficial",
			Description      = "Adds 24 new weapons. Unofficial update 1.18/1.19/1.20 repack.",
			Authors          = [ "Mr1k3", "fipil" ],
			Version          = "1.6.0",
			RequiredOnClient = true,
			RequiredOnServer = true,
			//Dependencies     = [ new("game", "1.20.0") ], // malformed, wrong property name (dependency instead of dependencies)
		},
		["wildcraftfruit_1.3.0.zip"] = new ModInfo() {
			Type         = EnumModType.Content,
			ModID        = "wildcraftfruit",
			Name         = "Wildcraft: Fruits and Nuts",
			Authors      = [ "gabb (code)", "Ледяная Соня (textures, models)", "CATASTEROID (some assets)", "L33tmaan (meal fixes)" ],
			Description  = "Adds an assortment of fruits, berries and nuts",
			Version      = "1.3.0",
			Dependencies = [ new("herbarium", "1.4.0") ],
		},
		["zzz_test1.cs"] = new ModInfo() {
			Type         = EnumModType.Code,
			ModID        = "test1",
			Name         = "Test1",
			Authors      = [ "copy\"girl" ],
			Description  = "Test\" ....",
			Version      = null,
			Dependencies = [ new("game", null), new("somemod", "1.2.3-pre.4") ],
		},
		["zzz_test2.cs"] = new ModInfo() {
			Type         = EnumModType.Code,
			ModID        = "test2",
			Name         = "Test2",
			Authors      = [ "copy\"girl" ],
			Contributors = [ "elo" ],
			Description  = "Test\" ....",
			Version      = null,
			Dependencies = [ new("game", null), new("somemod", "1.2.3-pre.4") ],
		},
	};

	[TestMethod]
	[DataRow("Bee_Keeper_v1.1.0.zip")]
	[DataRow("Recycle Metalwörk_v1.0.3.zip")]
	[DataRow("tailoringforall.zip")]
	[DataRow("Waxpress_1.0.0.zip")]
	[DataRow("wildcraftfruit_1.3.0.zip")]
	[DataRow("StepUp-v1.2.0.cs")]
	[DataRow("zzz_test1.cs")]
	[DataRow("zzz_test2.cs")]
	public void NoError(string inputFilePath)
	{
		var f = new FileInfo("TestInput/Valid/" + inputFilePath);
		Assert.IsTrue(f.Exists, "File seems to be missing.");

		Assert.IsTrue(ModPeek.TryGetModInfo(f, out var modInfo, PrintErrorToStdError), "Parsing failed.");

		Assert.IsTrue(ModPeek.ValidateModInfo(modInfo!, PrintErrorToStdError), "Validation failed.");

		Assert.IsTrue(_referenceInfos.TryGetValue(inputFilePath, out var reference), "Missing ref data");
		CompareInfos(reference, modInfo!);
	}

	[TestMethod]
	[DataRow("RemovedRustMonsterSpawns_v1.0.0.zip")]
	public void CriticalError(string inputFilePath)
	{
		var f = new FileInfo("TestInput/Defect/" + inputFilePath);
		Assert.IsTrue(f.Exists, "File seems to be missing.");

		var parseOk = ModPeek.TryGetModInfo(f, out var modInfo, PrintErrorToStdError);
		Assert.IsFalse(parseOk, "Expected parsing to fail.");
		Assert.IsNull(modInfo, "Expected fatal error to not produce modinfo.");
	}

	[TestMethod]
	[DataRow("TexturePack and CloneTool 1.0.4.zip")]
	[DataRow("warriordrink_v1.0.2.zip")]
	[DataRow("WeaponPackAlphaUnofficial_1.6.0.zip")]
	public void ParserError(string inputFilePath)
	{
		var f = new FileInfo("TestInput/Defect/" + inputFilePath);
		Assert.IsTrue(f.Exists, "File seems to be missing.");

		var parseOk = ModPeek.TryGetModInfo(f, out var modInfo, PrintErrorToStdError);
		if(modInfo != null) {
			ModPeek.ValidateModInfo(modInfo, PrintErrorToStdError);
		}

		//NOTE(Rennorb): Wired order so we get to see validation errors as well.
		Assert.IsFalse(parseOk, "Expected parsing to fail.");

		if(modInfo != null) {
			Assert.IsTrue(_referenceInfos.TryGetValue(inputFilePath, out var reference), "Missing ref data");
			CompareInfos(reference, modInfo);
		}
	}

	[TestMethod]
	[DataRow("jtraits_0.2.6.zip")]
	public void ValidationError(string inputFilePath)
	{
		var f = new FileInfo("TestInput/Defect/" + inputFilePath);
		Assert.IsTrue(f.Exists, "File seems to be missing.");

		Assert.IsTrue(ModPeek.TryGetModInfo(f, out var modInfo, PrintErrorToStdError), "Parsing failed.");

		Assert.IsFalse(ModPeek.ValidateModInfo(modInfo!, PrintErrorToStdError), "Validation should have failed.");

		Assert.IsTrue(_referenceInfos.TryGetValue(inputFilePath, out var reference), "Missing ref data");
		CompareInfos(reference, modInfo!);
	}

	static void PrintErrorToStdError(Error error)
    {
        Console.Error.WriteLine(ModPeek.FormatError(error));
    }

	public void CompareInfos(ModInfo reference, ModInfo info)
	{
		Assert.AreEqual(reference.Name, info.Name);
		Assert.AreEqual(reference.Version, info.Version);
		Assert.AreEqual(reference.NetworkVersion, info.NetworkVersion);
		Assert.AreEqual(reference.Description, info.Description);
		Assert.AreEqual(reference.Side, info.Side);
		Assert.AreEqual(reference.Type, info.Type);
		Assert.AreEqual(reference.ModID, info.ModID);
		AssertListEquals(reference.Authors, info.Authors);
		AssertListEquals(reference.Contributors, info.Contributors);
		AssertListEquals(reference.Dependencies, info.Dependencies, (a, b) => a.ToString() == b.ToString());
	}

	/// <summary> Replacement for CollectionAssert AreEqual that prints the collections. </summary>
	void AssertListEquals<T>(IReadOnlyList<T> reference, IReadOnlyList<T> list)
	{
		AssertListEquals(reference, list, (a, b) => Object.Equals(a, b));
	}
	/// <summary> Replacement for CollectionAssert AreEqual that prints the collections. </summary>
	void AssertListEquals<T>(IReadOnlyList<T> reference, IReadOnlyList<T> list, System.Func<T, T, bool> comparator)
	{
		var error = false;
		if(reference.Count != list.Count) {
			Console.Error.WriteLine($"List length mismatch: {reference.Count} != {list.Count}");
			error = true;
		}

		if(!error) for(int i = 0; i < reference.Count; i++) {
			if(!comparator(reference[i], list[i])) {
				Console.Error.WriteLine($"Element mismatch at index {i}: {reference[i]} != {list[i]}");
				error = true;
				break;
			}
		}

		if(error) {
			Console.Error.WriteLine("Reference:");
			for(int i = 0; i < reference.Count; i++) {
				Console.Error.Write($"[{i,3}]\t");
				Console.Error.WriteLine(reference[i]);
			}
			Console.Error.WriteLine("\nList:");
			for(int i = 0; i < list.Count; i++) {
				Console.Error.Write($"[{i,3}]\t");
				Console.Error.WriteLine(list[i]);
			}

			Assert.Fail("List mismatch.");
		}
	}
}
