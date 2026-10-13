#include "core/intrusive_list_walk.h"
#include <cassert>
#include <vector>

struct Node {
    Node(int value, Node* next = nullptr) : value(value), next(next) {}
    int value;
    Node* next;
};

static Node* nextNode(const Node& node) { return node.next; }

struct CountingVisitor {
    CountingVisitor() : count(0) {}
    void operator()(Node&) { ++count; }
    unsigned count;
};

int main() {
    Node single(11);
    CountingVisitor counter;
    spidey::core::visitIntrusiveList(&single, nextNode, counter);
    assert(counter.count == 1);

    std::vector<int> visits;
    spidey::core::visitIntrusiveList(static_cast<Node*>(nullptr), nextNode,
        [&visits](Node& node) { visits.push_back(node.value); });
    assert(visits.empty());

    Node third(3), second(2, &third), first(1, &second);
    Node movedList(9);
    spidey::core::visitIntrusiveList(&first, nextNode, [&visits, &movedList](Node& node) {
        visits.push_back(node.value);
        // Simulate suspension: reusing the link for another list must not
        // redirect this traversal into that list or skip the original next.
        node.next = &movedList;
    });
    assert((visits == std::vector<int>{1, 2, 3}));

    visits.clear();
    Node* head = new Node(1, new Node(2, new Node(3)));
    Node inserted(4);
    spidey::core::visitIntrusiveList(head, nextNode, [&visits, &head, &inserted](Node& node) {
        visits.push_back(node.value);
        if (node.value == 1) {
            inserted.next = node.next;
            head = &inserted;
        } else {
            inserted.next = node.next;
        }
        delete &node;
    });
    assert((visits == std::vector<int>{1, 2, 3}));
    assert(head == &inserted && inserted.next == nullptr);
    // Nodes introduced during one update wait for the next update.
    visits.clear();
    spidey::core::visitIntrusiveList(head, nextNode,
        [&visits](Node& node) { visits.push_back(node.value); });
    assert((visits == std::vector<int>{4}));
}
