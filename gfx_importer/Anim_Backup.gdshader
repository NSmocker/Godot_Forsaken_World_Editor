 shader_type spatial;


// KOMBINDERTE RENDER MODES

render_mode unshaded, cull_disabled, depth_draw_opaque, blend_mix, shadows_disabled;


// ==============================================================================

// === TEIL 0: MASTER CONTROLS & EDITOR PREVIEW ==============================

// ==============================================================================

uniform bool start_shader = true;

uniform float element_start_time = 0.0;

uniform float element_life_span = -1.0; 


group_uniforms Editor_Preview;

uniform bool  enable_auto_loop = true; 

uniform float global_loop_duration = 1.0; 

uniform bool  override_time = false;      

uniform float manual_time_slider : hint_range(0.0, 10.0, 0.01) = 0.0; 

group_uniforms;


// ==============================================================================

// === TEIL 1: TEXTUR & BILLBOARD ============================================

// ==============================================================================

uniform sampler2D albedo_tex : source_color, filter_linear_mipmap_anisotropic;

uniform vec4 albedo_color : source_color = vec4(1.0);

uniform int h_frames = 1;

uniform int v_frames = 1;

uniform float tex_interval = 1000.0;

uniform bool billboard_y = false;

uniform bool is_id_101_billboard = false; // <<< NEU: Das Voll-Billboard!


// ==============================================================================

// === TEIL 2: CONTROL-TYPES =================================================

// ==============================================================================

group_uniforms CtrlType_100;

uniform int   c100_CtrlType = -1; 

uniform float c100_StartTime = 0.0;

uniform float c100_EndTime = -1.0;

uniform vec3  c100_Dir = vec3(0.0, 1.0, 0.0);

uniform float c100_Vel = 0.0;

uniform float c100_Acc = 0.0;


group_uniforms CtrlType_101;

uniform int   c101_CtrlType = -1;

uniform float c101_StartTime = 0.0;

uniform float c101_EndTime = -1.0;

uniform float c101_Vel = 0.0;

uniform float c101_Acc = 0.0;


group_uniforms CtrlType_102;

uniform int   c102_CtrlType = -1;

uniform float c102_StartTime = 0.0;

uniform float c102_EndTime = -1.0;

uniform vec3  c102_Pos = vec3(0.0);           

uniform vec3  c102_Axis = vec3(0.0, 1.0, 0.0);

uniform float c102_Vel = 0.0;

uniform float c102_Acc = 0.0;



group_uniforms;


// ==============================================================================

// === HILFSFUNKTIONEN =======================================================

// ==============================================================================

mat3 construct_rotation_matrix(vec3 axis, float angle) {

    axis = normalize(axis);

    float s = sin(angle);

    float c = cos(angle);

    float oc = 1.0 - c;

    return mat3(

        vec3(oc * axis.x * axis.x + c,           oc * axis.x * axis.y - axis.z * s,  oc * axis.z * axis.x + axis.y * s),

        vec3(oc * axis.x * axis.y + axis.z * s,  oc * axis.y * axis.y + c,           oc * axis.y * axis.z - axis.x * s),

        vec3(oc * axis.z * axis.x - axis.y * s,  oc * axis.y * axis.z + axis.x * s,  oc * axis.z * axis.z + c)

    );

}


float calculate_main_time(float global_time) {

    if (override_time) return manual_time_slider;

    float synched_global_time = global_time;

    if (enable_auto_loop && global_loop_duration > 0.0 && element_life_span > 0.0) {

        synched_global_time = mod(global_time, global_loop_duration + 1.0); 

    }

    return synched_global_time - element_start_time;

}


// ==============================================================================

// === VERTEX FUNKTION =======================================================

// ==============================================================================

void vertex() {

    if (start_shader) {

        float main_time = calculate_main_time(TIME);

        

        if (main_time >= 0.0 && (element_life_span < 0.0 || main_time <= element_life_span)) {

            

            // ----------------------------------------------------------------------

            // SCHRITT 1: LOKALE VERTEX BEWEGUNGEN (Ctrl_101 & Ctrl_102)

            // Diese drehen sich absichtlich mit dem Billboard!

            // ----------------------------------------------------------------------

            

            // CtrlType_101 (2D Sprite Spin)

            if (c101_CtrlType == 101) {

                float t = main_time - c101_StartTime;

                if (t >= 0.0) {

                    if (c101_EndTime > 0.0) t = mod(t, c101_EndTime);

                    float current_angle = (c101_Vel * t) + (0.5 * c101_Acc * t * t);

                    float s = sin(current_angle);

                    float c = cos(current_angle);

                    mat3 rot_z = mat3(

                        vec3(c, -s, 0.0),

                        vec3(s,  c, 0.0),

                        vec3(0.0, 0.0, 1.0)

                    );

                    VERTEX = rot_z * VERTEX;

                }

            }


// CtrlType_102 (Lokale Pivot Orbit Rotation)

            if (c102_CtrlType == 102) {

                float t = main_time - c102_StartTime;

                if (t >= 0.0) {

                    if (c102_EndTime > 0.0) t = mod(t, c102_EndTime);

                    if (length(c102_Axis) > 0.001) {

                        float current_angle = (c102_Vel * t) + (0.5 * c102_Acc * t * t);

                        

                        // 1. ORBIT: Berechne nur die Verschiebung um den Pivot (nutzt die volle Achse)

                        vec3 local_axis = normalize((inverse(MODEL_MATRIX) * vec4(c102_Axis, 0.0)).xyz);

                        mat3 rot_matrix = construct_rotation_matrix(local_axis, current_angle);

                        vec3 orbit_translation = (rot_matrix * -c102_Pos) + c102_Pos;

                        

                        // 2. SPIN: Berechne die Eigendrehung des Meshes

                        vec3 spin_axis = c102_Axis;

                        

                        // Wenn Y-Billboard aktiv ist, löschen wir den Y-Spin, 

                        // damit das Objekt weiterhin zur Kamera schaut. X und Z bleiben erhalten!

                        if (billboard_y) {

                            spin_axis.y = 0.0;

                        }

                        

                        // Bei einem vollen Billboard bricht auch die X-Rotation den Effekt. 

                        // Hier lassen wir nur die Z-Rotation (Roll) zu.

                        if (is_id_101_billboard) {

                            spin_axis.x = 0.0;

                            spin_axis.y = 0.0;

                        }

                        

                        // Standard-Identitätsmatrix (keine Drehung)

                        mat3 spin_matrix = mat3(1.0); 

                        

                        // Nur rotieren, wenn nach dem Filter noch eine Achse übrig ist

                        if (length(spin_axis) > 0.001) {

                            vec3 local_spin_axis = normalize((inverse(MODEL_MATRIX) * vec4(spin_axis, 0.0)).xyz);

                            spin_matrix = construct_rotation_matrix(local_spin_axis, current_angle);

                        }

                        

                        // 3. KOMBINATION: Wende den gefilterten Spin und den vollen Orbit an

                        VERTEX = (spin_matrix * VERTEX) + orbit_translation;

                    }

                }

            }

            // ----------------------------------------------------------------------

            // SCHRITT 2: BILLBOARDING (Die Kamera Ausrichtung aufbauen)

            // ----------------------------------------------------------------------

            if (is_id_101_billboard) {

                float scale_x = length(MODEL_MATRIX[0].xyz);

                float scale_y = length(MODEL_MATRIX[1].xyz);

                float scale_z = length(MODEL_MATRIX[2].xyz);

                mat4 billboard_matrix = mat4(

                    vec4(normalize(INV_VIEW_MATRIX[0].xyz) * scale_x, 0.0),

                    vec4(normalize(INV_VIEW_MATRIX[1].xyz) * scale_y, 0.0),

                    vec4(normalize(INV_VIEW_MATRIX[2].xyz) * scale_z, 0.0),

                    vec4(MODEL_MATRIX[3].xyz, 1.0)

                );

                MODELVIEW_MATRIX = VIEW_MATRIX * billboard_matrix;

                MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);

                

            } else if (billboard_y) {

                float scale_x = length(MODEL_MATRIX[0].xyz);

                float scale_y = length(MODEL_MATRIX[1].xyz);

                float scale_z = length(MODEL_MATRIX[2].xyz);

                mat4 billboard_matrix = mat4(

                    vec4(normalize(INV_VIEW_MATRIX[0].xyz) * scale_x, 0.0),

                    vec4(0.0, scale_y, 0.0, 0.0),

                    vec4(normalize(INV_VIEW_MATRIX[2].xyz) * scale_z, 0.0),

                    vec4(MODEL_MATRIX[3].xyz, 1.0)

                );

                MODELVIEW_MATRIX = VIEW_MATRIX * billboard_matrix;

                MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);

            }


            // ----------------------------------------------------------------------

            // SCHRITT 3: GLOBALE BEWEGUNGEN (Ctrl_100)

            // Passiert NACH dem Billboard und ignoriert jede Kamera-Rotation

            // ----------------------------------------------------------------------

            if (c100_CtrlType == 100) {

                float t = main_time - c100_StartTime;

                if (t >= 0.0) {

                    if (c100_EndTime > 0.0) t = mod(t, c100_EndTime);

                    if (length(c100_Dir) > 0.001) {

                        // Wie viel Meter hat es zurückgelegt?

                        float dist = (c100_Vel * t) + (0.5 * c100_Acc * t * t);

                        

                        // Dies ist unser absoluter Flugvektor im globalen Raum

                        vec3 global_offset = c100_Dir * dist;

                        

                        // Wir zwingen die Position des Objekts in der MODELVIEW_MATRIX sich parallel zur globalen Welt zu bewegen, nicht lokal zum eigenen Mesh!

                        MODELVIEW_MATRIX[3].xyz += (VIEW_MATRIX * vec4(global_offset, 0.0)).xyz;

                    }

                }

            }

            

        }

    }

}


// ==============================================================================

// === FRAGMENT FUNKTION =====================================================

// ==============================================================================

void fragment() {

    if (!start_shader) {

        ALPHA = 0.0;

    } else {

        float main_time = calculate_main_time(TIME);

        if (main_time < 0.0 || (element_life_span > 0.0 && main_time > element_life_span)) {

            ALPHA = 0.0;

        } else {

            float total_angelica_units = main_time * 1000.0;

            int total_frames = max(1, h_frames * v_frames);

            int current_frame = int(total_angelica_units / max(1.0, tex_interval)) % total_frames;

            

            vec2 frame_size = vec2(1.0 / float(max(1, h_frames)), 1.0 / float(max(1, v_frames)));

            vec2 uv_offset = vec2(

                float(current_frame % h_frames) * frame_size.x, 

                float(current_frame / h_frames) * frame_size.y

            );

            

            vec4 tex = texture(albedo_tex, uv_offset + UV * frame_size);

            ALBEDO = tex.rgb * albedo_color.rgb;

            ALPHA = tex.a * albedo_color.a;

        }

    }

} 