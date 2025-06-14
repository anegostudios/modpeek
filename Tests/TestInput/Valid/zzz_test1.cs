
//
//NOTE(Rennorb): This is not a valid mod, its only here to test the attribute parser.
//

[assembly: ModInfo("Test1", 
	Version = "0.0.1",
	Description = @"Test"" ....",
	Website = "",
	Authors = new []{ "copy\"girl" })]
[assembly: ModDependency("game", "*")]
[assembly: ModDependency("somemod", "1.2.3-pre.4")]
