// Everything drawn in its own colours on the lit world: Maren, the drowned, objects, gore. The engine's ambient
// (CanvasModulate) and its lights act on it as usual; here it also takes the light bounced from the lamps and
// fires around it (the radiance buffer), so a body by a kiln goes warm on the kiln side of the room, and the
// story-focus tint drifts it toward the colour of what Maren is looking at.
shader_type canvas_item;

uniform sampler2D radiance : filter_linear, repeat_disable, hint_default_black;
uniform float indirect = 0.6;
uniform vec3 ambient_mod = vec3(0.62);
uniform vec3 tint : source_color = vec3(1.0);

void fragment() {
	vec4 c = COLOR;
	vec3 bounce = texture(radiance, SCREEN_UV).rgb * indirect;
	COLOR = vec4(c.rgb * tint + c.rgb * bounce / max(ambient_mod, vec3(0.05)), c.a);
}
