﻿using Newtonsoft.Json.Linq;

namespace VintageStory.ModPeek.Errors;

//NOTE(Rennorb): These TryParse and Validate functions don't fast-fail, instead they report every warning they can detect.
// We do this so that we are able to report all warnings at once, and a user doesn't have to go though multiple cycles of 
// validating if there are multiple issues with the ModInfo.

public enum Severity : byte { Fatal, Warning }

public abstract class Error(Severity severity) {
    public Severity Severity = severity;
}
public class MalformedArchive(Exception exception) : Error(Severity.Fatal) {
    public Exception Exception = exception;
}
public class MalformedJson(Exception exception) : Error(Severity.Fatal) {
    public Exception Exception = exception;
}
public class MissingAssemblyAttribute(string attributeName) : Error(Severity.Fatal) {
    public string AttributeName = attributeName;
}
public class MissingFileInArchiveRoot(string fileName, bool likelyCompressedDirectory) : Error(Severity.Fatal) {
    public string FileName                  = fileName;
    public bool   LikelyCompressedDirectory = likelyCompressedDirectory;
}
/// <summary> Can occur when a document gets parsed into primitive types. </summary>
public class PrimitiveParsingFailure(string targetProperty, string expectedType, string malformedInput) : Error(Severity.Warning) {
    public string TargetProperty  = targetProperty;
    public string ExpectedType    = expectedType;
    public string MalformedInput  = malformedInput;
}
/// <summary> Can occur when a string gets parsed into a different type. </summary>
public class StringParsingFailure(string targetProperty, string expectedType, string malformedInput) : Error(Severity.Warning) {
    public string TargetProperty  = targetProperty;
    public string ExpectedType    = expectedType;
    public string MalformedInput  = malformedInput;
}
public class MissingRequiredProperty(string targetStructure, string propertyName) : Error(Severity.Fatal) {
    public string TargetStructure = targetStructure;
    public string PropertyName    = propertyName;
}
public class MissingDependencyModID() : Error(Severity.Warning) {
}
public class UnexpectedProperty(string propertyName, string propertyValue) : Error(Severity.Warning) {
    public string PropertyName  = propertyName;
    public string PropertyValue = propertyValue;
}
public class UnexpectedValue(string targetProperty, string expected, string given) : Error(Severity.Warning) {
    public string TargetProperty = targetProperty;
    public string Expected       = expected;
    public string Given          = given;
}
public class UnexpectedJsonPropertyType(string targetProperty, JTokenType expectedType, JToken given) : Error(Severity.Warning) {
    public string     TargetProperty = targetProperty;
    public JTokenType ExpectedType   = expectedType;
    public JToken     Given          = given;
}
public class UnexpectedJsonRootType(JTokenType expectedType, JToken given) : Error(Severity.Fatal) {
    public JTokenType ExpectedType   = expectedType;
    public JToken     Given          = given;
}
public class MalformedPrimaryModID(string malformedInput) : Error(Severity.Fatal) {
    public string MalformedInput = malformedInput;
}
public class MalformedDependencyModID(string malformedInput) : Error(Severity.Warning) {
    public string MalformedInput = malformedInput;
}
public class MalformedPrimaryVersion(string malformedInput) : Error(Severity.Fatal) {
    public string MalformedInput = malformedInput;
}
public class MalformedNetworkVersion(string malformedInput) : Error(Severity.Fatal) {
    public string MalformedInput = malformedInput;
}
public class MalformedDependencyVersion(string dependency, string malformedInput) : Error(Severity.Warning) {
    public string Dependency     = dependency;
    public string MalformedInput = malformedInput;
}
public class ModIDGenerationFailure(Exception exception, string malformedInput) : Error(Severity.Fatal) {
    public Exception Exception = exception;
    public string MalformedInput = malformedInput;
}
public class MalformedAuthorName(string malformedInput) : Error(Severity.Warning) {
    public string MalformedInput = malformedInput;
}
public class MalformedContributorName(string malformedInput) : Error(Severity.Warning) {
    public string MalformedInput = malformedInput;
}

