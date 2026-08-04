class Solution {
public:
    int climbStairs(int n) {
        if (n <= 2) return 1;
        int previous = 1;
        int current = 1;
        for (int step = 3; step <= n; ++step) {
            int next = previous + current;
            previous = current;
            current = next;
        }
        return current;
    }
};
