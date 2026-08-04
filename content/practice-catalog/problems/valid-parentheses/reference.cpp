class Solution {
public:
    bool isValid(string s) {
        vector<char> stack;
        for (char value : s) {
            if (value == '(' || value == '[' || value == '{') {
                stack.push_back(value);
                continue;
            }
            if (stack.empty()) return false;
            char open = stack.back();
            stack.pop_back();
            if ((value == ')' && open != '(') ||
                (value == ']' && open != '[') ||
                (value == '}' && open != '{')) return false;
        }
        return stack.empty();
    }
};
