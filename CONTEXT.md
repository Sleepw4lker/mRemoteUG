# mRemoteUG

A tabbed remote-connections manager for RDP and SSH. A fork of mRemoteNG maintained by Uwe
Gradenegger; it exists to be fast, lean and modern — HiDPI, dark mode, current .NET — and
small enough that one person can maintain it. See [docs/adr/](docs/adr/) for the decisions
that shaped it.

This file is a glossary, nothing else. Terms are defined as the project means them, not as
the inherited type names spell them; where the two disagree the type name is noted so the
code is still findable.

## Language

### The saved side

**Connection**:
A saved record describing how to reach one remote machine — host, protocol, credentials,
display settings. It is data, not something that is running. Type: `ConnectionInfo`.
_Avoid_: session (a Connection is not live), entry, host.

**Folder**:
A Connection that contains other Connections instead of describing a machine of its own.
Folders carry settings that their children can inherit. Type: `ContainerInfo`.
_Avoid_: group, container, node.

**Inheritance**:
A child Connection taking a setting's value from its parent Folder rather than storing its
own. Per-property, not all-or-nothing. Type: `ConnectionInfoInheritance`.

**Default Connection**:
The template whose values a newly created Connection starts from. A singleton, not a member
of the tree. Type: `DefaultConnectionInfo`.

**PuTTY Profile**:
A session saved in PuTTY's own registry key, readable by this application so PuTTY's stored
settings can be reused. Nothing to do with a live connection, despite the type name.
Type: `PuttySessionInfo`.
_Avoid_: PuTTY session (collides with Session below).

### The live side

**Session**:
One live remote connection: established, running, and capable of going down on its own.
Exactly one Session per open Tab. This is the word the teardown code turns on — a Session is
the thing that has to be *down* before its control can be disposed.
_Avoid_: connection (that is the saved record), tab (that is where it is shown).

**Protocol**:
The driver for one Session — what connects, disconnects and owns the native control.
Base type: `ProtocolBase`; `RdpProtocol` and `PuttyBase` derive from it.
_Avoid_: client, transport.

**Protocol Type**:
Which kind of remote access a Connection asks for: RDP, SSH, Telnet, rlogin or RAW.
A property of a Connection; distinct from the Protocol that later serves it.
Type: `ProtocolType`.

**Hosted Control**:
The native control a Protocol draws into — the RDP ActiveX control, or the reparented PuTTY
window. It outlives its Tab: see [ADR-0005](docs/adr/0005-park-rdp-controls-before-disposing.md).
_Avoid_: OCX (only the RDP one is an OCX), view.

**Parked Control**:
A Hosted Control whose Tab has closed but whose Session has not yet gone down, held on an
off-screen form until it can be disposed.

### The window

**Tab**:
Where one Session is shown. Closing a Tab ends its Session.
_Avoid_: window, panel.

**Panel**:
A container of Tabs, of which there may be several side by side. A Panel is chosen when a
Connection is opened; it is not itself a Session or a Tab.
_Avoid_: dock, pane.

**Tool Window**:
One of the fixed docked windows around the Panels — connection tree, config, notifications.
Part of the main window's static layout, never a Tab.
_Avoid_: panel, dock window.

**Connection Tree**:
The Tool Window showing the Folders and Connections. Presents the saved side only; a
Connection shown there may or may not have a Session. Type: `ConnectionTree`.

**Node**:
A Connection's or Folder's position in the Connection Tree — the presentation, not the
record. Use *Connection* when you mean the record even if you reached it by clicking a Node.
_Avoid_: item, row.
### The artwork

**Glyph**:
A named mark on a menu, toolbar or list. Authored at 16 logical pixels and shipped at every
size on the Ladder; usually a Mask, so it follows the theme. Type: `Glyphs`.
_Avoid_: icon, image, bitmap.

**Connection Icon**:
The mark a user picks for a saved Connection, stored by name in the connections file and
offered by the picker. Shares the Ladder, the tint and the cache with a Glyph, but is a
separate set with its own names. Type: `ConnectionIcon`.
_Avoid_: glyph, logo.

**Window Icon**:
The multi-frame `.ico` on a form's title bar, in Alt-Tab and on the taskbar. Windows picks
the frame, not this application, so it is never tinted and never a Mask. Type: `WindowIcons`.
_Avoid_: glyph, app icon.

**Brand Mark**:
A mark identifying the product itself, as opposed to one of its functions. **There is still no
brand.** What the executable carries is `App_Icon` — generated from the Manifest like any other
Window Icon, and chosen only because it is the mark WinForms already draws on the window, so the
file and the running application stop disagreeing. The main window and session windows assign
nothing, and the installer uses the bitmaps WiX ships. The mRemoteNG marks that used to fill
these roles were removed rather than replaced; this entry exists so that absence reads as a
decision rather than an oversight.

**Ladder**:
The fixed device sizes artwork exists at: 16, 20, 24, 28, 32, 40, 48, 64. A request snaps
*up*. These are what a 16 logical pixel glyph becomes at the scale factors Windows offers.
_Avoid_: scale, DPI steps.

**Mask** / **Fixed**:
A Mask ships colourless — white, with the shape in the alpha channel — and is tinted at load
to the theme foreground. A Fixed glyph carries meaning in its colour (an error is red, a
warning amber) and is never tinted. Every entry in the Manifest is one or the other.

**Badge**:
A small mark composited onto the corner of another, such as the green dot on a connected
Node. Generated already inset, so compositing it is a straight 1:1 draw.
_Avoid_: overlay, status icon.

**Manifest**:
`src/mRemoteUG/Resources/icon-manifest.json` — the single list of what artwork exists.
Nothing is a Glyph, Connection Icon or Window Icon unless it is in there. Everything else,
including `Resources.g.cs`, is generated from it by `Tools/IconGen`.

**Legacy Name**:
A Connection Icon name from an older build that heals to a current one when read. The file on
disk keeps the old string; nothing is rewritten behind the user.
