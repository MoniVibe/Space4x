# Presentation Binding Samples

Two graybox binding sets that mirror the defaults baked into `PresentationBindingSamples`:

- **GrayboxMinimal**: capsule companions + simple ring/ping FX and UI pulse, palette-neutral for quick perf runs.
- **GrayboxFancy**: halo/palette-6 companions + brighter ping/UI pulses and a softer chime SFX placeholder.

Usage:
- Set runtime config `presentation.binding.sample` to `graybox-minimal` or `graybox-fancy` (via `RuntimeConfigRegistry` console or config file).
- `PresentationBindingSampleBootstrapSystem` will seed a `PresentationBindingReference` blob at startup using the selected set.

JSON files below mirror the baked bindings so designers can clone/extend them into authored assets if needed.
