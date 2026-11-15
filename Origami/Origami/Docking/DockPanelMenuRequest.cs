// This file is part of the Prowl Game Engine
// Licensed under the MIT License. See the LICENSE file in the project root for details.

using Prowl.Vector;

namespace Prowl.OrigamiUI;

/// <summary>
/// A click on a leaf's "..." panel menu button (see <see cref="DockSpace.PanelMenu"/>): the dock space, the leaf whose
/// button it was, the floating window holding that leaf (null when docked), and the button's screen rect.
/// </summary>
public sealed record DockPanelMenuRequest(DockSpace Space, DockNode Leaf, FloatingWindow? Window, Rect Button);
