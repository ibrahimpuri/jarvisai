// Water: depth tint with drifting caustics. Shaded, so caustics only show where light falls.
// And it reflects: near the surface the water mirrors what stands above it (the screen as drawn so far, flipped
// about the pool's own surface), broken by ripples and fading with depth, so the flooded Choir holds the rose
// window and Maren walks over her own reflection. Each pool's surface comes in through the vertex colour
// (WorldView.PaintWater), so pools at different heights each mirror about their own line.
shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float reflect_amt = 0.55;
uniform vec3 ambient_mod = vec3(0.62);
uniform float view_h = 360.0;
uniform vec4 top_col : source_color = vec4(0.16, 0.37, 0.43, 0.36);
uniform vec4 deep_col : source_color = vec4(0.03, 0.11, 0.16, 0.66);
uniform float top_y = 0.0;
uniform float depth = 220.0;
uniform float caustic_strength = 0.4;
uniform vec3 caustic_col = vec3(0.55, 0.82, 0.92);
varying vec2 wp;

void vertex() {
	wp = (MODEL_MATRIX * vec4(VERTEX, 0.0, 1.0)).xy;
}

float caus(vec2 p, float t) {
	vec2 i = p;
	float c = 1.0;
	float inten = 0.005;
	for (int n = 0; n < 4; n++) {
		float tt = t * (1.0 - (3.5 / float(n + 1)));
		i = p + vec2(cos(tt - i.x) + sin(tt + i.y), sin(tt - i.y) + cos(tt + i.x));
		c += 1.0 / length(vec2(p.x / (sin(i.x + tt) / inten), p.y / (cos(i.y + tt) / inten)));
	}
	c /= 4.0;
	c = 1.17 - pow(c, 1.4);
	return clamp(pow(abs(c), 8.0), 0.0, 1.0);
}

void fragment() {
	float d = clamp((wp.y - top_y) / depth, 0.0, 1.0);
	vec4 col = mix(top_col, deep_col, d);
	float c = caus(wp * 0.03, TIME * 0.55) * (1.0 - d * 0.75);
	col.rgb += caustic_col * c * caustic_strength;
	col.a = clamp(col.a + c * 0.12, 0.0, 1.0);
	if (reflect_amt > 0.0) {
		float sy = (COLOR.r + COLOR.g / 255.0) * 3.0 - 1.0;   // this pool's surface, in screen UV
		float below = SCREEN_UV.y - sy;
		float dpx = below * view_h;
		vec2 ruv = vec2(SCREEN_UV.x, sy - below);
		if (below > 0.0 && ruv.y > 0.0 && dpx < 160.0) {
			float rip = sin(wp.y * 0.9 + TIME * 2.2) * 0.6 + sin(wp.x * 0.11 + wp.y * 0.4 - TIME * 1.4) * 0.5;
			ruv.x += rip * (0.0012 + dpx * 0.00005);
			vec3 refl = textureLod(screen_tex, ruv, min(3.0, dpx * 0.03)).rgb;
			float k = reflect_amt * exp(-dpx / 64.0) * smoothstep(0.0, 0.03, ruv.y);
			// the water takes a little of its own colour into what it mirrors
			refl = mix(refl, top_col.rgb, 0.18) * 0.92;
			col.rgb = mix(col.rgb, refl / max(ambient_mod, vec3(0.05)), k);
			col.a = max(col.a, k * 0.92);
		}
	}
	COLOR = col;
}
