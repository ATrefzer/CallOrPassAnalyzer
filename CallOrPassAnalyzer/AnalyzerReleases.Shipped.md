; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0.0.3

### New Rules

 Rule ID | Category | Severity | Notes                               
---------|----------|----------|-------------------------------------
 COP001  | Design   | Warning  | Parameter is both called and passed
 COP002  | Design   | Warning  | Enum literal passed as argument when enum parameter is available. <br/><br/> Note: <br/> I added this rule after a larger refactoring to check if everything is consistent. May be useful sometimes but definitely no general rule!



