---
date: 2026-09-24
---

# Enable nullable reference types a directory at a time, with the build as the ratchet

Nothing in this project had ever been asked whether it can be null. There was no `<Nullable>`
property in any csproj or in `Directory.Build.props`, not one `#nullable` directive in 244 source
files, and no nullability attributes at all. Null-handling was entirely hand-written and in a
pre-C#7 dialect: 209 `== null`, 138 `!= null`, 216 `?.`, six `is null`, and zero each of
`is not null` and `??=`.

The reason to change that was not tidiness. The reflection-backed property inheritance in
`Connection/` routes 38 of `AbstractConnectionRecord`'s 41 properties through
`PropertyInfo.GetValue`, and it could hand a caller a null out of a property whose signature said
that was impossible; six getters then called `.Trim()` on the result. The XML deserializer
dereferenced some eighty possibly-absent attributes. Both are `NullReferenceException`s that no
test saw, in code paths this machine cannot exercise ([ADR-0013](0013-selftest-as-the-verification-mechanism.md):
no RDP host to connect to). The analyzer is the only mechanism available here that finds that class
of defect without a connected machine, which makes turning it on a diagnostic exercise rather than
a typing one.

## Measured, before deciding anything

Forcing `-p:Nullable=enable` on the command line, with all output redirected elsewhere so the
working tree is untouched, prices the whole job: **1,214 nullable warnings across 189 files** — 859
in `mRemoteV1` (110 of its 240 files) and 355 in `mRemoteNGTests`. `<Nullable>annotations</Nullable>`
adds **exactly zero** to the existing four warnings.

That measurement is worth keeping rather than taking once. Re-run after every slice, it is the only
honest answer to "how much is left", and it counts the work that is invisible to a normal build
because the files that would report it are not enabled yet:

```
dotnet build mRemoteV1.slnx -t:Rebuild -p:Nullable=enable -p:WarningsAsErrors= \
  -p:TreatWarningsAsErrors=false -p:BaseOutputPath=<scratch>\shadow-bin\ -nologo -v:n
```

`-p:WarningsAsErrors=` is not optional — without it the ratchet below turns the count into errors
and the build stops early. Leave `BaseIntermediateOutputPath` alone: redirecting it makes the
compiler glob the stale `obj\` trees, which fails with duplicate `TargetFrameworkAttribute`, and
because those are *declaration* errors the compiler then reports no body-level diagnostics at all —
a shadow build that reads 66 instead of 1,214. Each commit in the series records the number.

## `annotations` first, then one directory at a time

`Directory.Build.props` sets `<Nullable>annotations</Nullable>`. Annotations are honoured
everywhere, warnings are off, and a file opts into being checked by putting `#nullable enable` at
its top.

The decisive argument for `annotations` over leaving `Nullable` unset is not either of the obvious
ones. It is that **`disable` makes staging impossible**: writing `string?` in a file whose
annotation context is disabled is CS8632, which is inside the `nullable` warning group and so an
error under the ratchet. Under `disable` you cannot correct one declaration without enabling the
whole file and fixing every warning in it in the same commit. Under `annotations`, declarations and
bodies are separate knobs, which is what lets the contracts everything depends on be fixed ahead of
the bodies that consume them.

Three things about the mechanism were verified in throwaway projects rather than assumed, because
the whole order depends on them:

- A `[NotNull]` guard declared in an **un-enabled** file *is* honoured by an enabled caller. This is
  what makes a contracts-first pass possible at all.
- A `?` added to an un-enabled file's member warns **immediately** at an enabled call site, so work
  in one slice pays off in the next without revisiting it.
- An un-reviewed declaration that claims `string` and returns null warns **nowhere**. This is the
  price of `annotations`, and the reason the order follows the dependency direction rather than the
  warning counts.

That last point is why `Config/Serializers/` is annotated *after* `Connection/` and not before, even
though it is where the biggest single cluster of warnings was. Annotating
`new XAttribute("Panel", connectionInfo.Panel)` while `Panel` is still an unreviewed promise means
reviewing that line, seeing nothing, and marking clean the line that throws at run time. With the
connection record corrected first, the analyzer points at it.

## The build is the ratchet

`Directory.Build.props` also sets `<WarningsAsErrors>$(WarningsAsErrors);nullable</WarningsAsErrors>`.

There is no CI in this repository — no workflow files, and verification is local per ADR-0013 — so
the build is the only thing that can hold a line. Without it, a file that was cleaned in one commit
silently regresses in the next. With it, "it built" means "every enabled file is clean", and a
contract corrected late that breaks an already-enabled file fails **the commit that caused it**
rather than surfacing at the end.

The cost is real and worth stating: the property is unscoped, and `global.json` pins the SDK with
`rollForward: latestFeature`. A future feature band that adds or widens a nullable warning turns a
clean tree into a failing build with no source change, and with local-only verification that
presents as "the build broke and I changed nothing". Accepted rather than mitigated, because the
alternative is no enforcement at all; this paragraph is the mitigation.

**Amended 2026-09-24: there is CI now.** `.forgejo/workflows/build.yml` builds every push on a
Windows runner, so the ratchet is checked on a second machine as well as on this one. The design
is unchanged and deliberately so — the property stays in `Directory.Build.props`, not in the
workflow, so a local `dotnet build` still enforces the same line and the rule does not depend on a
runner being up. What changes is the failure above: an SDK feature band that widens a nullable
warning now breaks a push rather than waiting to surprise whoever next builds. See
[ADR-0025](0025-release-on-a-tag.md).

## `*.Designer.cs` is left completely alone

The obvious instinct is to put `#nullable disable` at the top of every generated designer file. It
would be wrong here, and the measurement says so: all 23 of them produce **zero** warnings under
`Nullable=enable`.

The reason is not that the code is clean. It is that **the compiler exempts them by filename**: a
partial named `Widget.Designer.cs` produces no CS8618 for the fields it declares, and the identical
file renamed to `WidgetParts.cs` does — reported, in both cases, against the constructor in the
hand-written half. So the intent behind the instinct is already satisfied by doing nothing, and
doing something would cost 23 line-ending-risky edits to turn *annotations* off in those files as
well, making designer-declared controls oblivious to their consumers instead of the truthful
non-null they are once `InitializeComponent` has run.

Two consequences to know. `UI/Controls/FilteredPropertyGrid/FilteredPropertyGrid.designer.cs` has a
lowercase `d`, so any glob written as `*.Designer.cs` misses it. And renaming a designer partial to
an ordinary filename would light up dozens of CS8618 at once — the exemption is in the name.

## What a fix is allowed to be

**A warning fixed by adding `!` is a warning not fixed.** Where the null-forgiving operator is
genuinely right it carries a comment saying why, and there are few of them: the pointer handed to
`Marshal.PtrToStringUni` came from `SecureStringToGlobalAllocUnicode`, which throws rather than
returning null, so that one is a fact about the platform and not a hope.

Beyond that:

- Prefer `required`, a field initializer or a constructor over `= null!`.
- `Try…` patterns get `[NotNullWhen(true)]` / `[MaybeNullWhen(false)]`, not a nullable return.
- Where one member's nullability is decided by another's value, say so with
  `[MemberNotNullWhen]` rather than making every caller re-check what it has already been told.
  `PasswordAuthenticator.Authenticate` returning true *is* the guarantee that
  `LastAuthenticatedPassword` is set.
- A real latent dereference is a bug: it gets a failing test first, and its own commit.

**NUnit fixtures.** `= null!` on a field assigned in `[SetUp]` is the obvious escape and mostly the
wrong one. NUnit builds a fixture once and reuses it, so a field no test reassigns can simply be
initialized where it is declared — which retires the `[SetUp]` that only existed to assign it, and
usually a `[TearDown]` that nulled it for nobody. Where a value is derived from other fields a field
initializer may not reference them (CS0236), and a constructor is the answer. `= null!` is for state
that genuinely has to be rebuilt per test, and then it says so.

**Generated settings and resource strings are trusted non-null.** `Settings.Designer.cs` and
`Language.Designer.cs` are regenerated and cannot be annotated, and `ConnectionInfo`'s seven
`Set*Defaults()` methods read some thirty `Settings.Default.ConDefault*` values straight into
properties. A corrupted `user.config` could hand back null with no warning in any baseline. If that
is wrong the failure is at startup, not at the use site, and chasing it per-call-site would add
noise everywhere to describe a condition that has never occurred.

## The one design decision the analyzer forced

`ConnectionInfo.TryGetInheritedPropertyValue` cast `PropertyInfo.GetValue(Parent)` to the property
type and returned `true` whatever came back. No annotation fixes that, because the shape is wrong:
there is no way for it to say "inherited, and legitimately null", and a null on the parent was
overriding a non-null value on the child.

**A null on the parent now means there is nothing to inherit.** For a caller, "the parent has no
value" and "this property is not inherited" are the same outcome, and the property type cannot
express a third. The alternative — making the return `TPropertyType?` and declaring some thirty-nine
getters as `string?` — is more literally honest and worse: it pushes a state nothing wants into the
property grid, both serializers, the CSV schema and the tree, and it would need an
`XAttribute`-guard at every save site. The invariant chosen instead is that no string property on
the record returns null, and the setters coerce to empty to make it true rather than merely claimed;
empty is what absence already meant there, since the constructor assigns `string.Empty` to
`Hostname` and 68 call sites test these with `IsNullOrEmpty`.

`PuttySessionInfo` is the exception, and deliberately so: it overrides eight of those properties as
plain auto-properties and `Panel` as `Parent?.Panel`, so it can still return null. It is never
serialized, which is why that is survivable.

## What this is not

It is not a guarantee that the annotations are true. Three layers carry these values where the
analyzer cannot follow: `PropertyInfo.GetValue`/`SetValue` in the inheritance lookup, `CopyFrom`,
and the `"ConDefault" + name` binding to `Settings.Default`; `PropertyDescriptor` in the filtered
property grid, which reads and writes connection properties without ever touching the C# property;
and the XML and CSV schemas, whose property names are string literals — the CSV schema is two
hard-coded semicolon-delimited lists kept in step by hand. `ConnectionInfo`'s own exclusion list
still named two properties that had ceased to exist. Annotating the record is necessary and not
sufficient, and in those three places only a test can tell you anything.
