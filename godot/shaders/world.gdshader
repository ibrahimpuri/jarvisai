// The lit world. Rooms are drawn in encoded value (red), accent (green) and glow (blue) and coloured here from
// the region's palette, the way Rain World colours its rooms; then they are lit, the way Hollow Knight lights
// its painted ground.
//
// - Relief: the drawing's own value is read as height, so every brick, lip and stone has a surface that turns
//   toward a lamp. Edges where the drawing ends fall away, which gives silhouettes a rim.
// - Direct light: every Light2D (lantern, sconces, fire, the region's key light) is shaded per pixel in light(),
//   with a little wrap so lamplight still reaches faces turned half away, and casts the engine's 2D shadows.
// - Wet stone: below the high-water mark the stone is wet and the lantern glints off it, so the tide line can be
//   read from across a room.
// - Indirect light: ambient arrives from above and below in the region's colours (a hemisphere), crevices hold
//   darkness, and light bounced from every lamp and fire in view arrives softly from the radiance buffer.
//
// The engine multiplies the base colour by the CanvasModulate (ambient_mod) and adds each light on top, so the
// indirect term and the glow are divided by it here to come out as authored.
shader_type canvas_item;

// ---- palette (the same names as palette.gdshader, so Palettes.Apply sets both) ----
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
uniform float alpha_mul = 1.0;
uniform float bright = 1.0;

// ---- lighting (set by LightingManager) ----
uniform bool tex_normals = true;        // derive relief from the drawing (baked layers); flat otherwise
uniform float relief = 2.2;
uniform vec3 sky_col : source_color = vec3(1.0);
uniform vec3 ground_col : source_color = vec3(0.7);
uniform vec3 ambient_mod = vec3(0.62);
uniform sampler2D radiance : filter_linear, repeat_disable, hint_default_black;
uniform float indirect = 0.6;
uniform float ao_amt = 0.6;
uniform float wet_line = 100000.0;      // world y below which the stone is wet
uniform float wet_amt = 0.0;            // how wet the room is everywhere (caves, the Gullet)
uniform float shininess = 28.0;
uniform float spec_amt = 0.6;
uniform float rim_amt = 0.35;

// ---- the surface's own material (art/materials, set per region by Art.cs) ----
// A tiling map of the stone's grain (red, around 0.5), where the accent grows on it (green) and its crevices (blue),
// and its normal map: what turns a flat painted wall into stone that a lamp rakes across.
uniform sampler2D mat_detail : filter_linear_mipmap, repeat_enable, hint_default_white;
uniform sampler2D mat_normal : filter_linear_mipmap, repeat_enable, hint_normal;
uniform float mat_amt = 0.0;            // 0: no material
uniform float mat_scale = 256.0;        // world px per tile
uniform float mat_grain = 0.5;
uniform float mat_bump = 1.0;
uniform float mat_accent = 0.4;
uniform float mat_cavity = 0.5;

varying vec2 wp;
varying vec3 alb;
varying float wet;

float bayer4(vec2 p) {
	int x = int(mod(p.x, 4.0)), y = int(mod(p.y, 4.0));
	int i = x + y * 4;
	float m[16] = float[16](0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0);
	return m[i] / 16.0;
}
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453); }
float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1, 0)), f.x), mix(hash(i + vec2(0, 1)), hash(i + vec2(1, 1)), f.x), f.y);
}

// height of the drawing at uv: its value where it is drawn, falling away where it ends
float height(sampler2D t, vec2 uv) {
	vec4 s = texture(t, uv);
	float a = s.a;
	float v = premul > 0.5 && a > 0.004 ? s.r / a : s.r;
	return a * (0.35 + v);
}

void vertex() {
	wp = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

void fragment() {
	vec4 e = COLOR;
	if (e.a < 0.004) discard;
	vec3 enc = premul > 0.5 ? e.rgb / e.a : e.rgb;
	float v = clamp(enc.r, 0.0, 1.0), acc = clamp(enc.g, 0.0, 1.0), gl = clamp(enc.b, 0.0, 1.0);

	// relief from the drawing
	vec3 n = vec3(0.0, 0.0, 1.0);
	float cavity = 0.0;
	if (tex_normals) {
		vec2 dx = vec2(TEXTURE_PIXEL_SIZE.x * 1.5, 0.0), dy = vec2(0.0, TEXTURE_PIXEL_SIZE.y * 1.5);
		float hc = height(TEXTURE, UV);
		float hl = height(TEXTURE, UV - dx), hr = height(TEXTURE, UV + dx);
		float hu = height(TEXTURE, UV - dy), hd = height(TEXTURE, UV + dy);
		n = normalize(vec3((hl - hr) * relief, (hu - hd) * relief, 1.0));
		// a crevice is lower than what surrounds it, and holds the dark
		cavity = clamp(((hl + hr + hu + hd) * 0.25 - hc) * 6.0, 0.0, 1.0);
	}

	// the surface's own material: its grain in the value, its moss or salt in the accent, its pits in the dark,
	// and its normal on top of the drawing's relief
	if (mat_amt > 0.0) {
		vec2 muv = wp / mat_scale;
		vec3 md = texture(mat_detail, muv).rgb;
		vec3 mn = texture(mat_normal, muv).xyz * 2.0 - 1.0;
		float g = (md.r - 0.5) * 2.0;
		v = clamp(v * (1.0 + g * mat_grain * mat_amt) + g * 0.015 * mat_amt, 0.0, 1.0);
		acc = clamp(acc + md.g * mat_accent * mat_amt * (1.0 - acc), 0.0, 1.0);
		cavity = clamp(cavity + md.b * mat_cavity * mat_amt, 0.0, 1.0);
		n = normalize(vec3(n.xy + mn.xy * mat_bump * mat_amt, n.z));
	}

	// the region's palette
	vec3 col = mix(shade_col, lit_col, v);
	col = mix(col, accent_col * (0.45 + 0.8 * v), acc);
	float hf = clamp((wp.y - fog_top) / max(1.0, fog_bottom - fog_top), 0.0, 1.0);
	float f = clamp(fog_amt + hf * hf * height_fog, 0.0, 1.0);
	col = mix(col, fog_col, f);
	alb = col * bright;

	// wet below the tide line, with runnels where water still drains down the stone
	float runnel = smoothstep(0.55, 0.9, noise(vec2(wp.x * 0.35, wp.y * 0.02)));
	wet = clamp(wet_amt + smoothstep(wet_line - 6.0, wet_line + 10.0, wp.y) * 0.9 + runnel * 0.35 * smoothstep(wet_line - 90.0, wet_line, wp.y), 0.0, 1.0);
	// wet stone is a little darker
	alb *= 1.0 - wet * 0.18;

	// ambient from above and below in the region's colours, darker in the crevices
	float up = clamp(-n.y * 0.5 + 0.5, 0.0, 1.0);
	vec3 hemi = mix(ground_col, sky_col, up);
	vec3 base = alb * hemi * (1.0 - cavity * ao_amt);
	// light bounced from every lamp and fire in view
	vec3 bounce = texture(radiance, SCREEN_UV).rgb * indirect;
	vec3 outc = base + alb * bounce / max(ambient_mod, vec3(0.05));
	// glow is light, not lit: it shows through the ambient dark as it was drawn
	outc += glow_col * gl * (1.0 - f * 0.5) / max(ambient_mod, vec3(0.05));
	if (dither > 0.0) outc += (bayer4(floor(FRAGCOORD.xy / 2.0)) - 0.5) * dither;
	COLOR = vec4(outc, e.a * alpha_mul);
	NORMAL = n;
}

void light() {
	vec3 L = LIGHT_DIRECTION;
	float ndl = dot(NORMAL, L);
	// wrapped diffuse: lamplight still reaches the faces turned half away
	float diff = clamp(ndl * 0.75 + 0.25, 0.0, 1.0);
	// a rim where the surface turns edge-on to the light
	float rim = pow(1.0 - clamp(NORMAL.z, 0.0, 1.0), 2.0) * clamp(ndl, 0.0, 1.0) * rim_amt;
	// wet stone glints
	vec3 h = normalize(L + vec3(0.0, 0.0, 1.0));
	float spec = pow(clamp(dot(NORMAL, h), 0.0, 1.0), shininess) * wet * spec_amt;
	vec3 lc = LIGHT_COLOR.rgb * LIGHT_ENERGY;
	LIGHT = vec4(lc * (alb * (diff + rim) + spec), LIGHT_COLOR.a);
}
