# NativeQuadtree
A Quadtree Native Collection for Unity DOTS. Octree version is here: https://github.com/marijnz/NativeOctree

<img src="Demo Scene.png" width="500"/></br

## Implementation
- It's a DOTS native container, meaning it's handling its own unmanaged memory and can be passed into jobs!
- It currently only supports the storing of points
- The bulk insertion is using morton codes. This allows very fast bulk insertion but causes an increasing (minor) overhead with an increased depth

## Performance
There's some very rudimentary performance tests included. With 20k elements on a 2k by 2k map, a max depth of 6 and 16 max elements per leaf. Burst enabled, ran on main thread on my 2015 MacBook Pro:</br>

- Job: Bulk insertion of all elements - Takes ~1ms
- Job: 1k queries on a 200m by 200m range - Takes ~1ms

With Burst disabled the tests are about 10x slower.

## Stability
The only tests test for performance so there's no real test coverage. I'm sure there's edge cases that are not caught. I would highly recommend writing more tests if you're planning to use the code in production.

## Potential future work / missing features
- Unit tests
- Support for basic shapes
- Other types of queries, such as raycasts
- Support individual adding and removing of elements
