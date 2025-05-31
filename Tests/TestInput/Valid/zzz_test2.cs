
//
//NOTE(Rennorb): This is not a valid mod, its only here to test the attribute parser.
//

[assembly: ModInfo("Test2", // Lets put a comment in the middle of the attribute.
	Description = """
	Test" ....
	""", // Verbatim string literals.
	Website = "",
	Authors = new []{ "copy\"girl" },
	Contributors // Some newlines in the expression for good measure.
	= new string[] { "elo" })]
[assembly: ModDependency("game", "*")]
[assembly: ModDependency("somemod", "1.2.3-pre.4")]
