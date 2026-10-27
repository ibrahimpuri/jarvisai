// Full-screen finishing pass: underwater refraction and tint, the region's colour grade, vignette, flashes, grain.
// The grade is set every frame by LightingManager, which blends it smoothly from one region's mood to the next:
// exposure, an S-curve for contrast, saturation, lift/gamma/gain (shadows/mids/highlights) and split toning,
// so the shore reads sea-glass cold, the Saltworks bone and amber, the Choir blue under gold.
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float under = 0.0;
uniform vec4 tint = vec4(0.0);
uniform float vignette = 0.42;
uniform float grain = 0.025;
uniform vec4 flash = vec4(0.0);
uniform float pressure = 0.0;
// the grade
uniform float exposure = 1.0;
uniform float contrast = 0.0;            // 0 flat, 1 a strong S-curve
uniform float saturation = 1.0;
uniform vec3 lift = vec3(0.0);
uniform vec3 gamma = vec3(1.0);
uniform vec3 gain = vec3(1.0);
uniform vec3 shadow_tint = vec3(0.0);
uniform vec3 high_tint = vec3(0.0);
uniform float focus_vignette = 0.0;      // extra framing while Maren lingers over something
uniform float bloom = 0.0;               // light bleeding into the air around lamps, shafts and glints
uniform float bloom_threshold = 0.55;
uniform float lean = 1.0;                // and the frame leaning in toward it: a slight magnification
uniform vec2 lean_focal = vec2(0.5);     // about this point (screen UV)

void fragment() {
	vec2 uv = lean_focal + (SCREEN_UV - lean_focal) / lean;
	if (under > 0.0) {
		uv += vec2(sin(uv.y * 38.0 + TIME * 2.4), cos(uv.x * 27.0 + TIME * 1.9)) * 0.0022 * under;
	}
	vec3 col = textureLod(screen_tex, uv, 0.0).rgb;
	if (bloom > 0.0) {
		// the screen's own mip chain is a cheap wide blur: what is bright in it bleeds out over its neighbours
		vec3 b = max(textureLod(screen_tex, uv, 2.0).rgb - bloom_threshold, 0.0) * 0.22
			+ max(textureLod(screen_tex, uv, 3.5).rgb - bloom_threshold, 0.0) * 0.33
			+ max(textureLod(screen_tex, uv, 5.0).rgb - bloom_threshold * 0.9, 0.0) * 0.45;
		col += b * bloom / (1.0 - bloom_threshold);
	}
	col = mix(col, tint.rgb, tint.a);

	col *= exposure;
	// S-curve: deepen the dark, keep the lamp pools from going flat
	vec3 s = clamp(col, 0.0, 1.0);
	col = mix(col, s * s * (3.0 - 2.0 * s), contrast);
	float lum = dot(col, vec3(0.299, 0.587, 0.114));
	col = mix(vec3(lum), col, saturation);
	// lift / gamma / gain
	col = max(col * gain + lift * (1.0 - col), 0.0);
	col = pow(col, 1.0 / max(gamma, vec3(0.05)));
	// split toning: one colour into the shadows, another into the lights
	lum = dot(col, vec3(0.299, 0.587, 0.114));
	col += shadow_tint * (1.0 - smoothstep(0.0, 0.45, lum)) + high_tint * smoothstep(0.45, 1.0, lum);

	vec2 q = UV - 0.5;
	q.x *= 16.0 / 9.0;
	float r = length(q);
	col *= 1.0 - smoothstep(0.38, 1.05, r) * (vignette + focus_vignette);
	col *= 1.0 - smoothstep(0.15, 0.95, r) * pressure * 0.75;
	col = mix(col, flash.rgb, flash.a);
	float n = fract(sin(dot(floor(FRAGCOORD.xy / 2.0) + floor(TIME * 24.0) * vec2(13.0, 7.0), vec2(12.9898, 78.233))) * 43758.5453);
	col += (n - 0.5) * grain;
	COLOR = vec4(col, 1.0);
}
