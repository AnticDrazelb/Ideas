class_name Surf
# One column of the cube, collapsed: the nearest solid cell to the camera.
var has := false
var d := 0        # depth in view space; larger is nearer the camera
var t := 0        # the cell's kind, as its character code
var w := Vector3.ZERO   # the world cell it came from
