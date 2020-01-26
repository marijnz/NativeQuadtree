# NativeQuadtree
A Quadtree Native Collection for Unity DOTS

It's WIP and shouldn't be used yet as there's still safety checks missing.

## Implementation
- It's a DOTS native data structure, meaning it's handling its own unmanaged memory and can be passed into jobs!
- It currently only supports the storing of points
- The bulk insertion is using morton codes. This allows very fast bulk insertion but causes an increasing (minor) overhead with a higher depth

## Performance
With 20k elements on a 2k by 2k map, a max depth of 6 and 16 max elements per leaf. Burst enabled, ran on main thread on my 2015 MacBook Pro:</br>

- Job: Bulk insertion of all elements - Takes ~.7ms
- Job: 1k queries on a 200m by 200m range - Takes ~1.5ms

With Burst disabled the tests are about 10x slower.