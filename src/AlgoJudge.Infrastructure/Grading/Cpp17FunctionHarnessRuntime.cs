namespace AlgoJudge.Infrastructure.Grading;

internal static class Cpp17FunctionHarnessRuntime
{
    public const string Source = """
        namespace algojudge_harness {
        struct JsonValue {
            enum class Kind { Null, Boolean, Number, String, Array, Object };
            Kind kind = Kind::Null;
            bool boolean_value = false;
            std::string text;
            std::vector<JsonValue> array_values;
            std::vector<std::pair<std::string, JsonValue>> object_values;

            const JsonValue& required(std::string_view name) const {
                if (kind != Kind::Object) throw std::runtime_error("expected object");
                const JsonValue* found = nullptr;
                for (const auto& item : object_values) {
                    if (item.first == name) {
                        if (found != nullptr) throw std::runtime_error("duplicate property");
                        found = &item.second;
                    }
                }
                if (found == nullptr) throw std::runtime_error("missing property");
                return *found;
            }

            void require_object_size(std::size_t expected) const {
                if (kind != Kind::Object || object_values.size() != expected)
                    throw std::runtime_error("unexpected object properties");
            }
        };

        class Parser {
        public:
            explicit Parser(std::string_view input) : input_(input) {}

            JsonValue parse_document() {
                skip_space();
                auto value = parse_value();
                skip_space();
                if (position_ != input_.size()) fail();
                return value;
            }

        private:
            std::string_view input_;
            std::size_t position_ = 0;

            [[noreturn]] static void fail() { throw std::runtime_error("invalid json"); }

            void skip_space() {
                while (position_ < input_.size()) {
                    char value = input_[position_];
                    if (value != ' ' && value != '\n' && value != '\r' && value != '\t') break;
                    ++position_;
                }
            }

            bool consume(char expected) {
                if (position_ < input_.size() && input_[position_] == expected) {
                    ++position_;
                    return true;
                }
                return false;
            }

            void expect(char expected) { if (!consume(expected)) fail(); }

            bool consume_literal(std::string_view literal) {
                if (input_.substr(position_, literal.size()) != literal) return false;
                position_ += literal.size();
                return true;
            }

            JsonValue parse_value() {
                if (position_ >= input_.size()) fail();
                if (input_[position_] == '{') return parse_object();
                if (input_[position_] == '[') return parse_array();
                if (input_[position_] == '"') {
                    JsonValue value;
                    value.kind = JsonValue::Kind::String;
                    value.text = parse_string();
                    return value;
                }
                if (consume_literal("true")) {
                    JsonValue value;
                    value.kind = JsonValue::Kind::Boolean;
                    value.boolean_value = true;
                    return value;
                }
                if (consume_literal("false")) {
                    JsonValue value;
                    value.kind = JsonValue::Kind::Boolean;
                    return value;
                }
                if (consume_literal("null")) return JsonValue{};
                return parse_number();
            }

            JsonValue parse_object() {
                expect('{');
                JsonValue value;
                value.kind = JsonValue::Kind::Object;
                skip_space();
                if (consume('}')) return value;
                while (true) {
                    if (position_ >= input_.size() || input_[position_] != '"') fail();
                    auto name = parse_string();
                    skip_space();
                    expect(':');
                    skip_space();
                    value.object_values.emplace_back(std::move(name), parse_value());
                    skip_space();
                    if (consume('}')) return value;
                    expect(',');
                    skip_space();
                }
            }

            JsonValue parse_array() {
                expect('[');
                JsonValue value;
                value.kind = JsonValue::Kind::Array;
                skip_space();
                if (consume(']')) return value;
                while (true) {
                    value.array_values.push_back(parse_value());
                    skip_space();
                    if (consume(']')) return value;
                    expect(',');
                    skip_space();
                }
            }

            JsonValue parse_number() {
                std::size_t start = position_;
                consume('-');
                if (consume('0')) {
                    if (position_ < input_.size() && input_[position_] >= '0' && input_[position_] <= '9') fail();
                } else {
                    if (position_ >= input_.size() || input_[position_] < '1' || input_[position_] > '9') fail();
                    while (position_ < input_.size() && input_[position_] >= '0' && input_[position_] <= '9') ++position_;
                }
                if (consume('.')) {
                    std::size_t digits = position_;
                    while (position_ < input_.size() && input_[position_] >= '0' && input_[position_] <= '9') ++position_;
                    if (digits == position_) fail();
                }
                if (position_ < input_.size() && (input_[position_] == 'e' || input_[position_] == 'E')) {
                    ++position_;
                    if (position_ < input_.size() && (input_[position_] == '+' || input_[position_] == '-')) ++position_;
                    std::size_t digits = position_;
                    while (position_ < input_.size() && input_[position_] >= '0' && input_[position_] <= '9') ++position_;
                    if (digits == position_) fail();
                }
                JsonValue value;
                value.kind = JsonValue::Kind::Number;
                value.text = std::string(input_.substr(start, position_ - start));
                return value;
            }

            static unsigned hex_value(char value) {
                if (value >= '0' && value <= '9') return static_cast<unsigned>(value - '0');
                if (value >= 'a' && value <= 'f') return static_cast<unsigned>(value - 'a' + 10);
                if (value >= 'A' && value <= 'F') return static_cast<unsigned>(value - 'A' + 10);
                fail();
            }

            unsigned parse_hex_quad() {
                if (position_ + 4 > input_.size()) fail();
                unsigned value = 0;
                for (int index = 0; index < 4; ++index)
                    value = (value << 4) | hex_value(input_[position_++]);
                return value;
            }

            static void append_utf8(std::string& output, unsigned code_point) {
                if (code_point <= 0x7f) output.push_back(static_cast<char>(code_point));
                else if (code_point <= 0x7ff) {
                    output.push_back(static_cast<char>(0xc0 | (code_point >> 6)));
                    output.push_back(static_cast<char>(0x80 | (code_point & 0x3f)));
                } else if (code_point <= 0xffff) {
                    output.push_back(static_cast<char>(0xe0 | (code_point >> 12)));
                    output.push_back(static_cast<char>(0x80 | ((code_point >> 6) & 0x3f)));
                    output.push_back(static_cast<char>(0x80 | (code_point & 0x3f)));
                } else {
                    output.push_back(static_cast<char>(0xf0 | (code_point >> 18)));
                    output.push_back(static_cast<char>(0x80 | ((code_point >> 12) & 0x3f)));
                    output.push_back(static_cast<char>(0x80 | ((code_point >> 6) & 0x3f)));
                    output.push_back(static_cast<char>(0x80 | (code_point & 0x3f)));
                }
            }

            std::string parse_string() {
                expect('"');
                std::string output;
                while (position_ < input_.size()) {
                    unsigned char value = static_cast<unsigned char>(input_[position_++]);
                    if (value == '"') return output;
                    if (value < 0x20) fail();
                    if (value != '\\') {
                        output.push_back(static_cast<char>(value));
                        continue;
                    }
                    if (position_ >= input_.size()) fail();
                    char escape = input_[position_++];
                    switch (escape) {
                        case '"': output.push_back('"'); break;
                        case '\\': output.push_back('\\'); break;
                        case '/': output.push_back('/'); break;
                        case 'b': output.push_back('\b'); break;
                        case 'f': output.push_back('\f'); break;
                        case 'n': output.push_back('\n'); break;
                        case 'r': output.push_back('\r'); break;
                        case 't': output.push_back('\t'); break;
                        case 'u': {
                            unsigned code_point = parse_hex_quad();
                            if (code_point >= 0xd800 && code_point <= 0xdbff) {
                                if (!consume('\\') || !consume('u')) fail();
                                unsigned low = parse_hex_quad();
                                if (low < 0xdc00 || low > 0xdfff) fail();
                                code_point = 0x10000 + ((code_point - 0xd800) << 10) + (low - 0xdc00);
                            } else if (code_point >= 0xdc00 && code_point <= 0xdfff) fail();
                            append_utf8(output, code_point);
                            break;
                        }
                        default: fail();
                    }
                }
                fail();
            }
        };

        template <typename T>
        T parse_integer(const JsonValue& value) {
            if (value.kind != JsonValue::Kind::Number || value.text.find_first_of(".eE") != std::string::npos)
                throw std::runtime_error("expected integer");
            T result{};
            auto conversion = std::from_chars(value.text.data(), value.text.data() + value.text.size(), result);
            if (conversion.ec != std::errc{} || conversion.ptr != value.text.data() + value.text.size())
                throw std::runtime_error("integer out of range");
            return result;
        }

        inline int as_int32(const JsonValue& value) { return parse_integer<int>(value); }
        inline long long as_int64(const JsonValue& value) { return parse_integer<long long>(value); }

        inline double as_double(const JsonValue& value) {
            if (value.kind != JsonValue::Kind::Number) throw std::runtime_error("expected number");
            char* end = nullptr;
            errno = 0;
            double result = std::strtod(value.text.c_str(), &end);
            if (errno == ERANGE || end != value.text.c_str() + value.text.size() || !std::isfinite(result))
                throw std::runtime_error("number out of range");
            return result;
        }

        inline bool as_boolean(const JsonValue& value) {
            if (value.kind != JsonValue::Kind::Boolean) throw std::runtime_error("expected boolean");
            return value.boolean_value;
        }

        inline std::string as_string(const JsonValue& value) {
            if (value.kind != JsonValue::Kind::String) throw std::runtime_error("expected string");
            return value.text;
        }

        template <typename T, T (*Convert)(const JsonValue&)>
        std::vector<T> as_array(const JsonValue& value) {
            if (value.kind != JsonValue::Kind::Array) throw std::runtime_error("expected array");
            std::vector<T> output;
            output.reserve(value.array_values.size());
            for (const auto& item : value.array_values) output.push_back(Convert(item));
            return output;
        }

        inline std::vector<int> as_int32_array(const JsonValue& value) { return as_array<int, as_int32>(value); }
        inline std::vector<long long> as_int64_array(const JsonValue& value) { return as_array<long long, as_int64>(value); }
        inline std::vector<double> as_double_array(const JsonValue& value) { return as_array<double, as_double>(value); }
        inline std::vector<bool> as_boolean_array(const JsonValue& value) { return as_array<bool, as_boolean>(value); }
        inline std::vector<std::string> as_string_array(const JsonValue& value) { return as_array<std::string, as_string>(value); }

        template <typename T>
        std::string serialize_integer(T value) {
            std::array<char, 32> buffer{};
            auto conversion = std::to_chars(buffer.data(), buffer.data() + buffer.size(), value);
            if (conversion.ec != std::errc{}) throw std::runtime_error("integer serialization failed");
            return std::string(buffer.data(), conversion.ptr);
        }

        inline std::string serialize(int value) { return serialize_integer(value); }
        inline std::string serialize(long long value) { return serialize_integer(value); }

        inline std::string serialize(double value) {
            if (!std::isfinite(value)) throw std::runtime_error("cannot serialize non-finite number");
            std::array<char, 64> buffer{};
            auto conversion = std::to_chars(
                buffer.data(), buffer.data() + buffer.size(), value,
                std::chars_format::general, std::numeric_limits<double>::max_digits10);
            if (conversion.ec != std::errc{}) throw std::runtime_error("number serialization failed");
            return std::string(buffer.data(), conversion.ptr);
        }

        inline std::string serialize(bool value) { return value ? "true" : "false"; }

        inline std::string serialize(const std::string& value) {
            static constexpr char hex[] = "0123456789abcdef";
            std::string output = "\"";
            for (unsigned char item : value) {
                switch (item) {
                    case '"': output += "\\\""; break;
                    case '\\': output += "\\\\"; break;
                    case '\b': output += "\\b"; break;
                    case '\f': output += "\\f"; break;
                    case '\n': output += "\\n"; break;
                    case '\r': output += "\\r"; break;
                    case '\t': output += "\\t"; break;
                    default:
                        if (item < 0x20) {
                            output += "\\u00";
                            output.push_back(hex[item >> 4]);
                            output.push_back(hex[item & 0x0f]);
                        } else output.push_back(static_cast<char>(item));
                }
            }
            output.push_back('"');
            return output;
        }

        template <typename T>
        std::string serialize(const std::vector<T>& values) {
            std::string output = "[";
            bool first = true;
            for (const auto& value : values) {
                if (!first) output.push_back(',');
                first = false;
                output += serialize(value);
            }
            output.push_back(']');
            return output;
        }
        }
        """;
}
