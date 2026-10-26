// Water: depth tint with drifting caustics. Shaded, so caustics only show where light falls.
shader_type canvas_item;
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
	COLOR = col;
}
