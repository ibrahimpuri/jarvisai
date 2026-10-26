// Full-screen finishing pass: underwater refraction and tint, colour grade, vignette, flashes, grain.
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float under = 0.0;
uniform vec4 tint = vec4(0.0);
uniform vec3 grade = vec3(0.12, 0.24, 0.31);
uniform float grade_mix = 0.06;
uniform float vignette = 0.55;
uniform float grain = 0.025;
uniform vec4 flash = vec4(0.0);
uniform float pressure = 0.0;

void fragment() {
	vec2 uv = SCREEN_UV;
	if (under > 0.0) {
		uv += vec2(sin(uv.y * 38.0 + TIME * 2.4), cos(uv.x * 27.0 + TIME * 1.9)) * 0.0022 * under;
	}
	vec3 col = texture(screen_tex, uv).rgb;
	col = mix(col, tint.rgb, tint.a);
	// gentle per-region grade toward its colour in the shadows
	float lum = dot(col, vec3(0.299, 0.587, 0.114));
	col = mix(col, grade * (0.6 + lum * 2.0), grade_mix * (1.0 - clamp(lum * 1.5, 0.0, 1.0)));
	vec2 q = UV - 0.5;
	q.x *= 16.0 / 9.0;
	float r = length(q);
	col *= 1.0 - smoothstep(0.38, 1.05, r) * vignette;
	col *= 1.0 - smoothstep(0.15, 0.95, r) * pressure * 0.75;
	col = mix(col, flash.rgb, flash.a);
	float n = fract(sin(dot(floor(FRAGCOORD.xy / 2.0) + floor(TIME * 24.0) * vec2(13.0, 7.0), vec2(12.9898, 78.233))) * 43758.5453);
	col += (n - 0.5) * grain;
	COLOR = vec4(col, 1.0);
}
