// Fog with a body. Two planes use this: one hangs between the depths and the playfield (it only shows where the
// ruined back wall opens onto the dark), and a thin haze sits in front of the world. Both drift slowly, gather low
// down and toward the water, and are lit from within by every lamp and fire in view (the radiance buffer), so light
// has volume in the air and a lantern carried through a hall leaves a glow in the mist around it.
// Premultiplied: the fog's own colour covers what is behind it, the scattered light adds on top.
shader_type canvas_item;
render_mode blend_premul_alpha, unshaded;

uniform sampler2D radiance : filter_linear, repeat_disable, hint_default_black;
uniform vec3 fog_col : source_color = vec3(0.3);
uniform float density = 0.2;
uniform float scatter = 0.6;
uniform vec2 cam = vec2(0.0);
uniform vec2 view = vec2(640.0, 360.0);
uniform float parallax = 0.85;      // how fast the fog drifts past as the camera moves
uniform float low_y = 360.0;        // world y where the fog is thickest
uniform float high_y = 0.0;         // world y above which it thins away
uniform float scale = 1.0;
// light shafts: soft beams falling at the key light's angle, the way light comes down through Hollow Knight's haze
uniform float shafts = 0.0;
uniform float shaft_angle = 0.3;        // radians from straight down, positive leaning right
uniform vec3 shaft_col : source_color = vec3(1.0);

float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}
float fbm(vec2 p) {
	float v = 0.0, a = 0.5;
	for (int i = 0; i < 4; i++) { v += a * noise(p); p = p * 2.03 + vec2(17.1, 9.2); a *= 0.5; }
	return v;
}

void fragment() {
	vec2 sp = SCREEN_UV * view;
	vec2 world = cam * parallax + sp;
	float t = TIME;
	float n = fbm(world * 0.0065 * scale + vec2(t * 0.012, t * 0.002)) * 0.65 + fbm(world * 0.019 * scale - vec2(t * 0.03, -t * 0.006)) * 0.35;
	float wy = cam.y + sp.y;
	float low = smoothstep(high_y, low_y, wy);
	float d = clamp(density * (0.25 + 0.95 * n) * (0.35 + 0.65 * low), 0.0, 0.85);
	vec3 L = texture(radiance, SCREEN_UV).rgb;
	vec3 c = fog_col * d + L * scatter * (0.35 + 0.9 * n) * (0.5 + d);
	if (shafts > 0.0) {
		vec2 dir = vec2(sin(shaft_angle), cos(shaft_angle));
		float u = dot(world, vec2(dir.y, -dir.x)) * 0.011;
		float beam = pow(noise(vec2(u * 2.6, t * 0.03)), 3.0) * smoothstep(0.25, 0.75, noise(vec2(u * 0.7, 11.0 + t * 0.01)));
		beam *= (1.0 - low * 0.8) * (0.6 + 0.8 * n);
		c += shaft_col * beam * shafts;
		d = clamp(d + beam * shafts * 0.25, 0.0, 0.9);
		// dust turning over in the beams, catching the light as it falls through
		vec2 mp = world * scale + vec2(sin(t * 0.21) * 9.0, t * 4.0);
		vec2 cell = floor(mp / 9.0), fc = fract(mp / 9.0);
		float hsh = hash(cell);
		vec2 at = vec2(hash(cell + 3.1), hash(cell + 7.7)) * 0.8 + 0.1;
		float mote = step(0.82, hsh) * smoothstep(0.07, 0.0, length(fc - at)) * (0.55 + 0.45 * sin(t * 1.3 + hsh * 60.0));
		c += (shaft_col * beam * shafts * 3.0 + L * 0.8) * mote;
	}
	COLOR = vec4(c, d);
}
