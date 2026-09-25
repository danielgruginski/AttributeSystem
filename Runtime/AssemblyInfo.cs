using System.Runtime.CompilerServices;

// The tests replace JsonDataLoader.ReadText to serve JSON without a Resources folder.
[assembly: InternalsVisibleTo("com.reactivesolutions.AttributeSystem.tests")]