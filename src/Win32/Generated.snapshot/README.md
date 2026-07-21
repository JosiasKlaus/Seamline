# Generated.snapshot

Snapshot of the CsWin32-generated interop sources (Microsoft.Windows.CsWin32 0.3.298,
from `NativeMethods.txt`), excluded from the build. Insurance per REWRITE_PLAN.md §3:
if the CsWin32 package ever dies or its generated identifier shapes drift, delete the
package reference, move these files into the project, and build from them forever.

Refresh after changing `NativeMethods.txt`:

```
dotnet build src/Win32 -c Release --no-incremental -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=obj\GeneratedEmit
```

then copy `obj/GeneratedEmit/**/*.g.cs` here.
