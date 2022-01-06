# Mesh Generation Job adapted from:
An amazing tutorial from Jasper Flink. Go join his Patreon.

## Procedural Meshes 03: Modified Grid 
[Jasper Flick](https://catlikecoding.com/jasper-flick/) / [Catlike Coding](https://catlikecoding.com).

[This is from the third tutorial in a series about procedural meshes.](https://catlikecoding.com/unity/tutorials/procedural-meshes/modified-grid/) It introduces a second way to generate a square grid and a way to animate it via a shader.

## Caveats

16b index types like PositionStream16 can only generate meshes up to 256*256. For bigger than that you need PositionStream32 which hasn't been made yet?
