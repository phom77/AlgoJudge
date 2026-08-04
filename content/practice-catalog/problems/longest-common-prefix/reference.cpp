class Solution {
public:
    string longestCommonPrefix(vector<string>& words) {
        string prefix = words[0];
        for (size_t index = 1; index < words.size(); ++index) {
            size_t length = 0;
            while (length < prefix.size() && length < words[index].size() &&
                   prefix[length] == words[index][length]) ++length;
            prefix.resize(length);
            if (prefix.empty()) break;
        }
        return prefix;
    }
};
