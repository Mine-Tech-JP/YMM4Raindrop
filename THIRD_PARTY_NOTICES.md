# Third-Party Notices

This file records third-party software used to build, verify, or host 雫と拭痕 – RainDrop & GlassWipe for YMM4.
The plugin package does not include the third-party DLLs listed below.
The product's own files are licensed separately under the Mozilla Public License 2.0; see `LICENSE.txt`.

Versions below are the versions resolved by the development YMM4 environment audited on 2026-09-04.

## YukkuriMovieMaker 4

- Version inspected: 4.56.0.1
- Role: application host and plugin API
- Distribution: not included in the `.ymme` package
- Rights: YukkuriMovieMaker 4 remains subject to its own terms. This project does not grant rights to YukkuriMovieMaker 4.

## Vortice.Windows components

- Components: Vortice.D3DCompiler, Vortice.Direct2D1, Vortice.Direct3D11, Vortice.DirectX, Vortice.DXGI
- Version: 2.1.8-beta
- Role: DirectX APIs supplied by the YMM4 host; D3DCompiler is also used by the build-only shader compiler
- Distribution: the component DLLs are not included in the `.ymme` package
- License expression in each versioned NuGet package: MIT
- Upstream: https://github.com/amerkoleci/Vortice.Windows

```text
MIT License

Copyright (c) Amer Koleci and Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Vortice.Mathematics

- Version: 1.4.12
- Role: math types supplied by the YMM4 host
- Distribution: not included in the `.ymme` package
- License expression in the versioned NuGet package: MIT
- Upstream license at audited commit: https://github.com/amerkoleci/Vortice.Mathematics/blob/407974e50374c984916476f38a76eb406ab09bc7/LICENSE

```text
MIT License

Copyright (c) Amer Koleci and contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## SharpGen runtime components

- Components: SharpGen.Runtime, SharpGen.Runtime.COM
- Version: 2.0.0-beta.10
- Role: COM and native interop runtime supplied by the YMM4 host
- Distribution: not included in the `.ymme` package
- License expression in each versioned NuGet package: MIT
- Upstream release tag: https://github.com/SharpGenTools/SharpGenTools/tree/v2.0.0-beta.10

```text
MIT License

Copyright (c) 2010-2017 Alexandre Mutel, 2017 Jeremy Koritzinsky

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Newtonsoft.Json

- Version: 13.0.4
- Role: verification project reference
- Distribution: not included in the `.ymme` package
- License expression and included license in the versioned NuGet package: MIT
- Upstream release tag: https://github.com/JamesNK/Newtonsoft.Json/tree/13.0.4

```text
MIT License

Copyright (c) 2007 James Newton-King

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## Microsoft D3DCompiler

- Component inspected: `D3DCompiler_47_cor3.dll`
- Role: shader compilation during the build only
- Distribution: not included in the `.ymme` package
- License identified by Microsoft's Windows component license information: Windows SDK License
- License information: https://github.com/dotnet/core/blob/main/license-information-windows.md
- Audited binary version: 10.0.26100.7705
- Audited binary SHA-256: `A05F99734F7C4822FEFC12B367AF21FD0976ED6608752FB1E1E80B6ECE7ECBBB`
- Company metadata in the audited binary: Microsoft Corporation
- Copyright metadata in the audited binary: © Microsoft Corporation. All rights reserved.

## Reference material

The YukkuriMovieMaker4PluginSamples repository was inspected at commit `8e06e7247bc4a9d870c604927559b8be9a8e4910`.
No repository license or reuse notice was present at that commit.
Automated comparison found no whole-file match, no matching project-specific comments, and no long project-specific HLSL match.
The remaining short matches were standard .NET/YMM4 project settings and API usage patterns.
The project owner stated that they did not copy, paste, or adapt code, comments, or HLSL from the official samples and did not instruct an AI system to do so; the internal operation of AI-assisted generation was not represented as an absolute guarantee.
The sample repository is not included in this project or its package.
