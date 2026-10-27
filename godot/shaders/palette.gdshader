// Palette mapping in the manner of Rain World: layers are drawn as value (red), accent (green) and glow
// (blue), and coloured here from the region's palette. Depth fades a layer into the fog colour, height
// thickens the fog toward the water, and a fine ordered dither gives the haze some grit. The foreground
// layer reads its texture through a small blur, as if out of focus.
shader_type canvas_item;

uniform vec3 shade_col : source_color = vec3(0.04);
uniform vec3 lit_col : source_color = vec3(0.5);
uniform vec3 fog_col : source_color = vec3(0.3);
uniform vec3 accent_col : source_color = vec3(0.5);
uniform vec3 glow_col : source_color = vec3(1.0);
uniform float fog_amt = 0.0;
uniform float height_fog = 0.0;
uniform float fog_top = 0.0;
uniform float fog_bottom = 360.0;
uniform float dither = 0.0;
uniform float premul = 1.0;
uniform float blur = 0.0;
uniform float alpha_mul = 1.0;
uniform float bright = 1.0;     // undoes the ambient darkening on layers that sit in the lit world
// the vault: depth planes darken toward the top of the room, so the light seems to come from below and the
// openings read as depth rather than as flat paper
uniform float vault = 0.0;
uniform float room_top = 0.0;
uniform float room_bottom = 360.0;
// foreground occlusion: the near plane thins to a soft window wherever it would hide Maren
uniform vec2 cut_pos = vec2(-10.0);   // her centre, in screen UV
uniform float cut_r = 0.0;            // radius, as a fraction of the screen height
uniform float cut_min = 0.3;          // what is left of the silhouette at the centre of the window

varying vec2 wp;

float bayer4(vec2 p) {
	int x = int(mod(p.x, 4.0)), y = int(mod(p.y, 4.0));
	int i = x + y * 4;
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return m[i] / 16.0;
}

void vertex() {
	wp = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

// Depth of field: a 12-tap Poisson disc, rotated per pixel so the blur reads as soft rather than doubled.
const vec2 DISC[12] = vec2[12](vec2(-0.326, -0.406), vec2(-0.840, -0.074), vec2(-0.696, 0.457), vec2(-0.203, 0.621),
	vec2(0.962, -0.195), vec2(0.473, -0.480), vec2(0.519, 0.767), vec2(0.185, -0.893),
	vec2(0.507, 0.064), vec2(0.896, 0.412), vec2(-0.322, -0.933), vec2(-0.792, -0.598));

void fragment() {
	vec4 e = COLOR;
	if (blur > 0.0) {
		vec2 px = TEXTURE_PIXEL_SIZE * blur;
		float ang = fract(sin(dot(FRAGCOORD.xy, vec2(12.9898, 78.233))) * 43758.5453) * 6.2832;
		mat2 rot = mat2(vec2(cos(ang), sin(ang)), vec2(-sin(ang), cos(ang)));
		e = texture(TEXTURE, UV) * 0.16;
		for (int i = 0; i < 12; i++) e += texture(TEXTURE, UV + rot * DISC[i] * px) * 0.07;
	}
	if (e.a < 0.004) discard;
	vec3 enc = premul > 0.5 ? e.rgb / e.a : e.rgb;
	float v = clamp(enc.r, 0.0, 1.0), acc = clamp(enc.g, 0.0, 1.0), gl = clamp(enc.b, 0.0, 1.0);
	vec3 col = mix(shade_col, lit_col, v);
	col = mix(col, accent_col * (0.45 + 0.8 * v), acc);
	float hf = clamp((wp.y - fog_top) / max(1.0, fog_bottom - fog_top), 0.0, 1.0);
	float f = clamp(fog_amt + hf * hf * height_fog, 0.0, 1.0);
	col = mix(col, fog_col, f);
	if (vault > 0.0) {
		float ry = clamp((wp.y - room_top) / max(1.0, room_bottom - room_top), 0.0, 1.0);
		col *= 1.0 - vault * pow(1.0 - ry, 1.7);
	}
	col *= bright;
	col += glow_col * gl * (1.0 - f * 0.5);
	if (dither > 0.0) col += (bayer4(floor(FRAGCOORD.xy / 2.0)) - 0.5) * dither;
	float a = e.a * alpha_mul;
	if (cut_r > 0.0) {
		vec2 q = (SCREEN_UV - cut_pos) * vec2(16.0 / 9.0, 1.0);
		a *= mix(cut_min, 1.0, smoothstep(cut_r * 0.45, cut_r, length(q)));
	}
	COLOR = vec4(col, a);
}
