#ifndef MILESTRO_CLASS_H
#define MILESTRO_CLASS_H

#define MILESTRO_DECLARE_NON_COPYABLE(className)                                                                         \
    className(const className&) = delete;                                                                              \
    className& operator=(const className&) = delete;

#endif //MILESTRO_MILESTRO_CLASS_H
