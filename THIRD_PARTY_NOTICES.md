# Third-party notices

ThinkControl redistributes selected third-party material and adapts selected open-source implementation techniques as described below.

## LibreHardwareMonitor

Project: LibreHardwareMonitor

Upstream: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor

License: Mozilla Public License 2.0 (MPL-2.0), with additional third-party components covered by the upstream project's own notices.

ThinkControl references the official `LibreHardwareMonitorLib` NuGet package for read-only hardware sensor discovery. ThinkControl does not present LibreHardwareMonitor as its own implementation and keeps Lenovo-specific control/write gates separate from generic sensor discovery.

Upstream license and third-party notices:

https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/LICENSE

https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/blob/master/THIRD-PARTY-NOTICES.txt

## PawnIO

Project: PawnIO

Upstream: https://github.com/namazso/PawnIO

License: GNU General Public License version 2 or later, with the additional upstream exception described in PawnIO's `COPYING`/README.

ThinkControl does not bundle or silently install the PawnIO driver/setup package. On supported hardware, the app can download the pinned official PawnIO installer only after an explicit user action, verifies its expected SHA-256 before launch, and then lets Windows handle the elevated installation. LibreHardwareMonitor can use an installed PawnIO provider for low-level sensor access; ThinkControl's verified X9 EC provider also requires PawnIO to be present before any direct EC control path is enabled.

Upstream license:

https://github.com/namazso/PawnIO/blob/master/COPYING

## Material Symbols

Project: Google Material Symbols / Material Design Icons

Upstream: https://github.com/google/material-design-icons

License: Apache License 2.0

ThinkControl uses selected icon paths from the official SVG assets and represents them as WPF `Geometry` resources. Material Symbols are not bundled as a font and ThinkControl does not require the Google Fonts service at runtime.

Upstream license:

https://github.com/google/material-design-icons/blob/master/LICENSE

## Buy Me a Coffee mark

Project: Buy Me a Coffee brand mark / Simple Icons representation

Brand kit: https://buymeacoffee.com/brand

Icon source: https://github.com/simple-icons/simple-icons

License for the Simple Icons geometry: CC0-1.0

ThinkControl uses the recognizable Buy Me a Coffee cup mark inside its support controls and the standard Buy Me a Coffee button in the README. The controls only open the external Buy Me a Coffee support page in the user's browser. Buy Me a Coffee and its marks are trademarks of their respective owner; their appearance in ThinkControl only identifies that external support link and does not imply endorsement.

## EdgeSlide

Project: EdgeSlide

Upstream: https://github.com/puttingpixelstogether-ops/edgeslide

License: MIT License

ThinkControl's Precision Touchpad input implementation was informed by and adapts selected Raw Input, HID descriptor parsing and HID interoperability techniques from EdgeSlide. ThinkControl's gesture recognition, action routing, cursor handling, haptic settings and WPF interface are implemented as ThinkControl-specific components.

MIT License

Copyright (c) 2026 Amazing SAS

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

Other external packages retain the licenses declared by their respective distributions and package metadata.
## Selected Figma typography and SVG rendering

IBM Plex Sans Regular and SemiBold are distributed under the SIL Open Font License 1.1. The full license is packaged in Assets/Fonts/LICENSE.txt. Source: https://github.com/IBM/plex.

Microsoft Fluent UI System Icons are distributed under the MIT license. Source: https://github.com/microsoft/fluentui-system-icons. Selected SVGs are preserved as assets, including the Figma-exported source glyphs.

SharpVectors.Wpf is distributed under the BSD 3-Clause license. Source: https://github.com/ElinamLLC/SharpVectors. It renders the packaged original SVGs in WPF.

## Microsoft Fluent System Icons license

MIT License

Copyright (c) 2020 Microsoft Corporation

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


## SharpVectors and bundled component licenses

﻿BSD 3-Clause License

Copyright (c) 2010 - 2026, Elinam LLC
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

* Redistributions of source code must retain the above copyright notice, this
  list of conditions and the following disclaimer.

* Redistributions in binary form must reproduce the above copyright notice,
  this list of conditions and the following disclaimer in the documentation
  and/or other materials provided with the distribution.

* Neither the name of the copyright holder nor the names of its
  contributors may be used to endorse or promote products derived from
  this software without specific prior written permission.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.


-------------------------------------------------------------------------------------------------------------------
Brotli License (https://github.com/google/brotli)
Copyright (c) 2009, 2010, 2013-2016 by the Brotli Authors.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in
all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.  IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
THE SOFTWARE.

-------------------------------------------------------------------------------------------------------------------
MinIoC License (https://github.com/microsoft/MinIoC)
MIT License

Copyright (c) Microsoft Corporation. All rights reserved.

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
SOFTWARE
