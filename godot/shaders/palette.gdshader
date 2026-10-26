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

void fragment() {
	vec4 e = COLOR;
	if (blur > 0.0) {
		vec2 px = TEXTURE_PIXEL_SIZE * blur;
		e = texture(TEXTURE, UV) * 0.2;
		e += (texture(TEXTURE, UV + vec2(px.x, 0.0)) + texture(TEXTURE, UV - vec2(px.x, 0.0)) + texture(TEXTURE, UV + vec2(0.0, px.y)) + texture(TEXTURE, UV - vec2(0.0, px.y))) * 0.125;
		e += (texture(TEXTURE, UV + px) + texture(TEXTURE, UV - px) + texture(TEXTURE, UV + vec2(px.x, -px.y)) + texture(TEXTURE, UV + vec2(-px.x, px.y))) * 0.075;
	}
	if (e.a < 0.004) discard;
	vec3 enc = premul > 0.5 ? e.rgb / e.a : e.rgb;
	float v = clamp(enc.r, 0.0, 1.0), acc = clamp(enc.g, 0.0, 1.0), gl = clamp(enc.b, 0.0, 1.0);
	vec3 col = mix(shade_col, lit_col, v);
	col = mix(col, accent_col * (0.45 + 0.8 * v), acc);
	float hf = clamp((wp.y - fog_top) / max(1.0, fog_bottom - fog_top), 0.0, 1.0);
	float f = clamp(fog_amt + hf * hf * height_fog, 0.0, 1.0);
	col = mix(col, fog_col, f);
	col *= bright;
	col += glow_col * gl * (1.0 - f * 0.5);
	if (dither > 0.0) col += (bayer4(floor(FRAGCOORD.xy / 2.0)) - 0.5) * dither;
	COLOR = vec4(col, e.a * alpha_mul);
}
